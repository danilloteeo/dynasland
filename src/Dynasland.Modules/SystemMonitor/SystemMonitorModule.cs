using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.SystemMonitor;

/// <summary>
/// Painel com CPU, memória, GPU, temperaturas e disco, aberto pelo atalho da tela inicial.
/// A leitura completa (1 s) só acontece enquanto o painel está visível; em segundo plano há apenas
/// uma verificação barata de memória e disco a cada 30 s para os avisos.
/// </summary>
public sealed class SystemMonitorModule(
    ISystemMetricsService metrics,
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IShortcutRegistry shortcuts,
    ILogger<SystemMonitorModule> logger) : IIslandModule
{
    public const string ModuleId = "system";
    public const string PanelActivityId = "system.panel";

    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AlertInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AlertDisplayDuration = TimeSpan.FromSeconds(6);

    private readonly Lock _lock = new();
    private readonly SystemAlertPolicy _alertPolicy = new();
    private SystemMonitorContent? _content;
    private IDisposable? _shortcut;
    private ITimer? _sampler;
    private ITimer? _alertTimer;

    public string Id => ModuleId;

    public string DisplayName => "Monitor do sistema";

    public string Description => "Uso de CPU, memória, GPU, temperaturas (quando disponíveis) e espaço em disco.";

    public static string AlertActivityId(SystemAlert alert) => $"system.alert.{alert.Key}";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _content = new SystemMonitorContent(OnPresentationChanged);
            _alertTimer = time.CreateTimer(_ => CheckAlerts(), null, TimeSpan.FromSeconds(10), AlertInterval);
        }

        _shortcut = shortcuts.Register(new IslandShortcut("system.panel", ModuleId, Glyphs.Diagnostic, "Sistema", OpenPanel));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _shortcut?.Dispose();
        _shortcut = null;
        lock (_lock)
        {
            _sampler?.Dispose();
            _alertTimer?.Dispose();
            _sampler = null;
            _alertTimer = null;
            _content = null;
        }

        return Task.CompletedTask;
    }

    public void OpenPanel()
    {
        if (_content is { } content)
        {
            bus.PostActivity(new IslandActivity(PanelActivityId, ModuleId, ActivityKind.Transient, content) { OpenInteractive = true });
        }
    }

    private void OnPresentationChanged(IslandState? state)
    {
        lock (_lock)
        {
            var visible = state is IslandState.Interactive or IslandState.Expanded;
            if (visible && _sampler is null && _content is not null)
            {
                _sampler = time.CreateTimer(_ => Sample(), null, TimeSpan.Zero, SampleInterval);
            }
            else if (!visible && _sampler is not null)
            {
                _sampler.Dispose();
                _sampler = null;
            }
        }
    }

    private void Sample()
    {
        var content = _content;
        if (content is null)
        {
            return;
        }

        try
        {
            var sample = metrics.Sample();
            ui.Post(() => content.Apply(sample));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao ler métricas do sistema.");
        }
    }

    private void CheckAlerts()
    {
        IReadOnlyList<SystemAlert> alerts;
        try
        {
            var memory = metrics.GetMemory();
            var disks = metrics.GetDisks();
            lock (_lock)
            {
                alerts = _alertPolicy.Evaluate(memory, disks, time.GetUtcNow());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha na verificação de memória e disco.");
            return;
        }

        foreach (var alert in alerts)
        {
            bus.PostActivity(new IslandActivity(AlertActivityId(alert), ModuleId, ActivityKind.Transient, new SystemAlertContent(alert))
            {
                Priority = alert.IsSevere ? ActivityPriority.High : ActivityPriority.Normal,
                Duration = AlertDisplayDuration,
            });
        }
    }
}
