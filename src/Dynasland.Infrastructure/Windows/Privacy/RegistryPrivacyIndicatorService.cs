using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Dynasland.Infrastructure.Windows.Privacy;

/// <summary>
/// Microfone e câmera em uso, lidos dos registros de uso que o próprio Windows mantém (os mesmos do
/// ícone de privacidade da bandeja), sem administrador nem drivers. Consulta a cada 2 segundos numa
/// thread do pool, só enquanto a opção está ligada; cada leitura são algumas dezenas de chaves do HKCU.
/// </summary>
public sealed class RegistryPrivacyIndicatorService : IPrivacyIndicatorService, IDisposable
{
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IIslandSettingsService _settings;
    private readonly ILogger<RegistryPrivacyIndicatorService> _logger;
    private readonly Lock _lock = new();
    private Timer? _timer;
    private int _polling;
    private bool _disposed;

    public RegistryPrivacyIndicatorService(IIslandSettingsService settings, ILogger<RegistryPrivacyIndicatorService> logger)
    {
        _settings = settings;
        _logger = logger;
        settings.Changed += OnSettingsChanged;
        Apply(settings.Current.ShowPrivacyIndicators);
    }

    public PrivacyUsage Current { get; private set; } = PrivacyUsage.None;

    public event EventHandler<PrivacyUsage>? Changed;

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _settings.Changed -= OnSettingsChanged;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void OnSettingsChanged(object? sender, IslandSettings settings) => Apply(settings.ShowPrivacyIndicators);

    /// <summary>Liga ou desliga a consulta; desligada, os indicadores somem.</summary>
    private void Apply(bool enabled)
    {
        lock (_lock)
        {
            if (_disposed || enabled == (_timer is not null))
            {
                return;
            }

            if (enabled)
            {
                _timer = new Timer(_ => Poll(), null, TimeSpan.Zero, PollInterval);
                return;
            }

            _timer?.Dispose();
            _timer = null;
        }

        Publish(PrivacyUsage.None, onlyWhileEnabled: false);
    }

    private void Poll()
    {
        // Uma leitura por vez: se o registro demorar, o próximo tique é descartado.
        if (Interlocked.Exchange(ref _polling, 1) == 1)
        {
            return;
        }

        try
        {
            Publish(PrivacyUsageEvaluator.Evaluate(ConsentStoreReader.Read(_logger)), onlyWhileEnabled: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler o uso de microfone e câmera.");
        }
        finally
        {
            Volatile.Write(ref _polling, 0);
        }
    }

    private void Publish(PrivacyUsage usage, bool onlyWhileEnabled)
    {
        lock (_lock)
        {
            // Desligado durante a leitura: não reacende os indicadores.
            if ((onlyWhileEnabled && _timer is null) || usage.Equals(Current))
            {
                return;
            }

            Current = usage;
        }

        Changed?.Invoke(this, usage);
    }
}

/// <summary>Lê <c>HKCU\...\CapabilityAccessManager\ConsentStore\{microphone,webcam}</c>.</summary>
internal static class ConsentStoreReader
{
    internal const string RootPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";
    private const string NonPackaged = "NonPackaged";

    private static readonly (PrivacyCapability Capability, string Name)[] Capabilities =
    [
        (PrivacyCapability.Microphone, "microphone"),
        (PrivacyCapability.Camera, "webcam"),
    ];

    /// <summary>Registros de todos os aplicativos; chaves ilegíveis são ignoradas.</summary>
    public static List<PrivacyUsageEntry> Read(ILogger? logger = null)
    {
        var entries = new List<PrivacyUsageEntry>();
        foreach (var (capability, name) in Capabilities)
        {
            try
            {
                using var root = Registry.CurrentUser.OpenSubKey($@"{RootPath}\{name}");
                if (root is null)
                {
                    continue;
                }

                // Empacotados ficam direto na chave; desktop, um nível abaixo, em NonPackaged.
                ReadApps(root, capability, entries, logger, skip: NonPackaged);
                using var desktop = root.OpenSubKey(NonPackaged);
                if (desktop is not null)
                {
                    ReadApps(desktop, capability, entries, logger, skip: null);
                }
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Falha ao ler {Capability} no registro.", name);
            }
        }

        return entries;
    }

    private static void ReadApps(RegistryKey parent, PrivacyCapability capability, List<PrivacyUsageEntry> entries, ILogger? logger, string? skip)
    {
        foreach (var appKey in parent.GetSubKeyNames())
        {
            if (skip is not null && string.Equals(appKey, skip, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using var app = parent.OpenSubKey(appKey);
                if (app is null)
                {
                    continue;
                }

                entries.Add(new PrivacyUsageEntry(capability, appKey, ReadFileTime(app, "LastUsedTimeStart"), ReadFileTime(app, "LastUsedTimeStop")));
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Falha ao ler o uso de {App}.", appKey);
            }
        }
    }

    /// <summary>QWORD (FILETIME); ausente ou de outro tipo conta como 0 (nunca).</summary>
    private static long ReadFileTime(RegistryKey key, string name) => key.GetValue(name) switch
    {
        long value => value,
        int value => value,
        _ => 0,
    };
}
