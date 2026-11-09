using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Windows.Downloads;

/// <summary>
/// Acompanha downloads de navegadores pela pasta Downloads, a partir dos arquivos temporários que
/// eles criam durante a transferência (Chrome, Edge, Opera, Brave: <c>.crdownload</c>; Firefox: <c>.part</c>).
/// </summary>
/// <remarks>
/// Navegadores não informam o tamanho total no arquivo temporário: este provedor sabe nome, bytes
/// recebidos e velocidade, mas não o progresso percentual nem o tempo restante.
/// A conclusão é detectada quando o temporário é renomeado para o nome final; se ele some sem
/// deixar o arquivo final, o download foi cancelado. O tamanho só é consultado (a cada segundo)
/// enquanto houver downloads em andamento.
/// </remarks>
public sealed class BrowserDownloadFolderProvider : IDownloadProvider, IDisposable
{
    public static readonly string[] TemporaryExtensions = [".crdownload", ".part", ".partial", ".download"];

    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DeletionGrace = TimeSpan.FromMilliseconds(600);
    private const double SpeedSmoothing = 0.35;

    private readonly string? _folder;
    private readonly TimeProvider _time;
    private readonly ILogger<BrowserDownloadFolderProvider> _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Tracked> _active = new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? _watcher;
    private ITimer? _sampler;

    public BrowserDownloadFolderProvider(TimeProvider time, ILogger<BrowserDownloadFolderProvider> logger)
        : this(KnownFolders.GetDownloads(), time, logger)
    {
    }

    internal BrowserDownloadFolderProvider(string? folder, TimeProvider time, ILogger<BrowserDownloadFolderProvider> logger)
    {
        _folder = folder;
        _time = time;
        _logger = logger;
    }

    public string Name => "Navegadores (pasta Downloads)";

    public event EventHandler<DownloadInfo>? Changed;

    public Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_folder) || !Directory.Exists(_folder))
        {
            return Task.FromResult(false);
        }

        _watcher = new FileSystemWatcher(_folder)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName,
        };
        _watcher.Created += (_, e) => OnCreated(e.FullPath);
        _watcher.Renamed += (_, e) => OnRenamed(e.OldFullPath, e.FullPath);
        _watcher.Deleted += (_, e) => OnDeleted(e.FullPath);
        _watcher.Error += (_, e) => _logger.LogWarning(e.GetException(), "Monitoramento da pasta Downloads interrompido.");
        _watcher.EnableRaisingEvents = true;

        // Downloads que já estavam em andamento quando o Dynasland abriu.
        foreach (var path in Directory.EnumerateFiles(_folder).Where(IsTemporary))
        {
            OnCreated(path);
        }

        return Task.FromResult(true);
    }

    public Task StopAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
        lock (_lock)
        {
            _sampler?.Dispose();
            _sampler = null;
            _active.Clear();
        }
    }

    public static bool IsTemporary(string path) =>
        TemporaryExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    /// <summary><c>foto.zip.crdownload</c> → <c>foto.zip</c>.</summary>
    public static string FinalPathOf(string temporaryPath) =>
        temporaryPath[..temporaryPath.LastIndexOf('.')];

    /// <summary>Chrome usa "Unconfirmed 123456.crdownload" até o usuário confirmar o nome.</summary>
    public static string DisplayNameOf(string temporaryPath)
    {
        var name = Path.GetFileName(FinalPathOf(temporaryPath));
        return name.StartsWith("Unconfirmed ", StringComparison.OrdinalIgnoreCase) ? "Novo download" : name;
    }

    private void OnCreated(string path)
    {
        if (!IsTemporary(path))
        {
            return;
        }

        Tracked tracked;
        lock (_lock)
        {
            if (_active.ContainsKey(path))
            {
                return;
            }

            tracked = new Tracked(Guid.NewGuid().ToString("N"), path, _time.GetUtcNow());
            _active[path] = tracked;
            EnsureSampler();
        }

        Publish(tracked, DownloadState.InProgress, null);
    }

    private void OnRenamed(string oldPath, string newPath)
    {
        var oldIsTemporary = IsTemporary(oldPath);
        var newIsTemporary = IsTemporary(newPath);

        if (!oldIsTemporary)
        {
            OnCreated(newPath);
            return;
        }

        Tracked? tracked;
        lock (_lock)
        {
            if (!_active.Remove(oldPath, out tracked))
            {
                tracked = null;
            }
            else if (newIsTemporary)
            {
                // "Unconfirmed 123.crdownload" → "arquivo.zip.crdownload": mesmo download, nome definitivo.
                tracked.TemporaryPath = newPath;
                _active[newPath] = tracked;
            }
        }

        if (tracked is null)
        {
            OnCreated(newPath);
        }
        else if (newIsTemporary)
        {
            Publish(tracked, DownloadState.InProgress, null);
        }
        else
        {
            Complete(tracked, newPath);
        }
    }

    private void OnDeleted(string path)
    {
        Tracked? tracked;
        lock (_lock)
        {
            _active.Remove(path, out tracked);
            if (_active.Count == 0)
            {
                _sampler?.Dispose();
                _sampler = null;
            }
        }

        if (tracked is null)
        {
            return;
        }

        // Alguns navegadores apagam o temporário logo depois de gravar o arquivo final.
        var finalPath = FinalPathOf(path);
        _ = Task.Delay(DeletionGrace, _time).ContinueWith(_ =>
        {
            var final = new FileInfo(finalPath);
            if (final.Exists && final.Length > 0 && final.LastWriteTimeUtc >= tracked.StartedAt.UtcDateTime.AddSeconds(-2))
            {
                Complete(tracked, finalPath);
            }
            else
            {
                Publish(tracked, DownloadState.Cancelled, null);
            }
        }, TaskScheduler.Default);
    }

    private void Complete(Tracked tracked, string finalPath)
    {
        lock (_lock)
        {
            if (_active.Count == 0)
            {
                _sampler?.Dispose();
                _sampler = null;
            }
        }

        long size;
        try
        {
            size = new FileInfo(finalPath).Length;
        }
        catch (IOException)
        {
            size = tracked.LastBytes;
        }

        tracked.LastBytes = size;
        Publish(tracked, DownloadState.Completed, finalPath);
    }

    /// <summary>Inicia a amostragem de tamanho. Chamar com <see cref="_lock"/> adquirido.</summary>
    private void EnsureSampler() =>
        _sampler ??= _time.CreateTimer(_ => SampleActive(), null, SampleInterval, SampleInterval);

    private void SampleActive()
    {
        Tracked[] active;
        lock (_lock)
        {
            active = [.. _active.Values];
        }

        var now = _time.GetUtcNow();
        foreach (var tracked in active)
        {
            long bytes;
            try
            {
                bytes = new FileInfo(tracked.TemporaryPath).Length;
            }
            catch (IOException)
            {
                continue;
            }

            var elapsed = (now - tracked.LastSampleAt).TotalSeconds;
            if (elapsed > 0 && tracked.LastSampleAt != default)
            {
                var instant = Math.Max(0, bytes - tracked.LastBytes) / elapsed;
                tracked.Speed = tracked.Speed is { } previous ? previous + SpeedSmoothing * (instant - previous) : instant;
            }

            tracked.LastBytes = bytes;
            tracked.LastSampleAt = now;
            Publish(tracked, DownloadState.InProgress, null);
        }
    }

    private void Publish(Tracked tracked, DownloadState state, string? finalPath)
    {
        var info = new DownloadInfo(
            tracked.Id,
            Name,
            finalPath is null ? DisplayNameOf(tracked.TemporaryPath) : Path.GetFileName(finalPath),
            finalPath ?? tracked.TemporaryPath,
            tracked.LastBytes,
            TotalBytes: null,
            state == DownloadState.InProgress ? tracked.Speed : null,
            state,
            tracked.StartedAt);

        Changed?.Invoke(this, info);
    }

    private sealed class Tracked(string id, string temporaryPath, DateTimeOffset startedAt)
    {
        public string Id { get; } = id;

        public string TemporaryPath { get; set; } = temporaryPath;

        public DateTimeOffset StartedAt { get; } = startedAt;

        public long LastBytes { get; set; }

        public DateTimeOffset LastSampleAt { get; set; }

        public double? Speed { get; set; }
    }
}
