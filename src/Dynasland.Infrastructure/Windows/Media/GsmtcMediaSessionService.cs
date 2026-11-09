using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.Media;
using Windows.Media.Control;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using SessionManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace Dynasland.Infrastructure.Windows.Media;

/// <summary>
/// Todas as sessões de mídia do Windows via Global System Media Transport Controls (ex.: Spotify
/// tocando música e um navegador exibindo um filme ao mesmo tempo). Totalmente orientado a eventos.
/// </summary>
/// <remarks>
/// Sessões não têm identificador próprio: usamos o AppUserModelId (com sufixo se o mesmo aplicativo
/// tiver mais de uma sessão) e reaproveitamos o identificador enquanto o objeto da sessão existir.
/// Chamadas WinRT são feitas no pool (MTA), porque objetos obtidos lá não funcionam a partir da UI.
/// </remarks>
public sealed class GsmtcMediaSessionService(TimeProvider time, ILogger<GsmtcMediaSessionService> logger) : IMediaSessionService
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Lock _lock = new();
    private readonly List<Tracked> _tracked = [];

    private SessionManager? _manager;
    private IReadOnlyList<MediaSnapshot> _sessions = [];

    public IReadOnlyList<MediaSnapshot> Sessions
    {
        get
        {
            lock (_lock)
            {
                return _sessions;
            }
        }
    }

    public string? CurrentSessionId { get; private set; }

    public event EventHandler? Changed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var manager = await SessionManager.RequestAsync().AsTask(cancellationToken);
        lock (_lock)
        {
            _manager = manager;
        }

        manager.SessionsChanged += OnSessionsChanged;
        manager.CurrentSessionChanged += OnCurrentSessionChanged;
        await RefreshAsync(resync: true);
    }

    public Task StopAsync()
    {
        lock (_lock)
        {
            if (_manager is not null)
            {
                _manager.SessionsChanged -= OnSessionsChanged;
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                _manager = null;
            }

            foreach (var tracked in _tracked)
            {
                Detach(tracked.Session);
            }

            _tracked.Clear();
            _sessions = [];
            CurrentSessionId = null;
        }

        return Task.CompletedTask;
    }

    public Task<bool> PlayAsync(string sessionId) => Control(sessionId, s => s.TryPlayAsync());

    public Task<bool> PauseAsync(string sessionId) => Control(sessionId, s => s.TryPauseAsync());

    public Task<bool> SkipNextAsync(string sessionId) => Control(sessionId, s => s.TrySkipNextAsync());

    public Task<bool> SkipPreviousAsync(string sessionId) => Control(sessionId, s => s.TrySkipPreviousAsync());

    private async Task<bool> Control(string sessionId, Func<Session, global::Windows.Foundation.IAsyncOperation<bool>> command)
    {
        Session? session;
        lock (_lock)
        {
            session = _tracked.FirstOrDefault(t => t.Id == sessionId)?.Session;
        }

        if (session is null)
        {
            return false;
        }

        try
        {
            // A sessão foi obtida numa thread MTA; comandos vindos da thread de UI (STA) não têm efeito.
            return await Task.Run(async () => await command(session));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Comando de mídia recusado por {Session}.", sessionId);
            return false;
        }
    }

    private void OnSessionsChanged(SessionManager sender, SessionsChangedEventArgs args) => _ = RefreshAsync(resync: true);

    private void OnCurrentSessionChanged(SessionManager sender, CurrentSessionChangedEventArgs args) => _ = RefreshAsync(resync: false);

    private void OnSessionChanged(Session sender, object args) => _ = RefreshAsync(resync: false);

    /// <summary>Relê todas as sessões. Com <paramref name="resync"/>, também detecta sessões novas e encerradas.</summary>
    private async Task RefreshAsync(bool resync)
    {
        await _refreshGate.WaitAsync();
        try
        {
            SessionManager? manager;
            lock (_lock)
            {
                manager = _manager;
            }

            if (manager is null)
            {
                return;
            }

            if (resync)
            {
                Resync(manager.GetSessions());
            }

            List<Tracked> tracked;
            lock (_lock)
            {
                tracked = [.. _tracked];
            }

            // O Windows devolve objetos .NET diferentes para a mesma sessão a cada chamada,
            // então a sessão atual é identificada pelo aplicativo (a primeira sessão dele).
            var currentAppId = manager.GetCurrentSession()?.SourceAppUserModelId;
            var currentId = tracked.FirstOrDefault(t => currentAppId is not null && t.AppId == currentAppId)?.Id;
            var snapshots = new List<MediaSnapshot>();
            foreach (var entry in tracked)
            {
                try
                {
                    if (await ReadAsync(entry) is { } snapshot)
                    {
                        snapshots.Add(snapshot);
                    }
                }
                catch (Exception ex)
                {
                    // Sessão encerrada durante a leitura; a próxima notificação corrige a lista.
                    logger.LogDebug(ex, "Falha ao ler a sessão {Session}.", entry.Id);
                }
            }

            // A sessão atual do Windows primeiro, depois as que estão tocando.
            snapshots = [.. snapshots
                .OrderByDescending(s => s.Id == currentId)
                .ThenByDescending(s => s.IsPlaying)];

            Publish(snapshots, currentId);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Falha ao ler as sessões de mídia.");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Refaz a lista de sessões acompanhadas. A identidade é o aplicativo (com sufixo <c>#2</c>, <c>#3</c>...
    /// para várias sessões do mesmo app), porque os objetos .NET mudam a cada consulta; a capa já lida
    /// é preservada por identificador.
    /// </summary>
    private void Resync(IReadOnlyList<Session> sessions)
    {
        lock (_lock)
        {
            var previous = _tracked.ToDictionary(t => t.Id);
            foreach (var old in _tracked)
            {
                Detach(old.Session);
            }

            _tracked.Clear();
            foreach (var session in sessions)
            {
                var appId = session.SourceAppUserModelId ?? string.Empty;
                var id = appId;
                for (var n = 2; _tracked.Any(t => t.Id == id); n++)
                {
                    id = $"{appId}#{n}";
                }

                session.MediaPropertiesChanged += OnSessionChanged;
                session.PlaybackInfoChanged += OnSessionChanged;
                session.TimelinePropertiesChanged += OnSessionChanged;

                var tracked = new Tracked(id, appId, session);
                if (previous.TryGetValue(id, out var before))
                {
                    tracked.ThumbnailTrackKey = before.ThumbnailTrackKey;
                    tracked.Thumbnail = before.Thumbnail;
                }

                _tracked.Add(tracked);
            }
        }
    }

    private void Detach(Session session)
    {
        session.MediaPropertiesChanged -= OnSessionChanged;
        session.PlaybackInfoChanged -= OnSessionChanged;
        session.TimelinePropertiesChanged -= OnSessionChanged;
    }

    private async Task<MediaSnapshot?> ReadAsync(Tracked entry)
    {
        var session = entry.Session;
        var properties = await session.TryGetMediaPropertiesAsync();
        if (properties is null || string.IsNullOrWhiteSpace(properties.Title))
        {
            return null;
        }

        var playback = session.GetPlaybackInfo();
        var appId = session.SourceAppUserModelId ?? string.Empty;

        var trackKey = $"{properties.Title}\u001f{properties.Artist}\u001f{properties.AlbumTitle}";
        if (trackKey != entry.ThumbnailTrackKey)
        {
            entry.ThumbnailTrackKey = trackKey;
            entry.Thumbnail = await TryReadThumbnailAsync(properties.Thumbnail);
        }

        var (position, duration, updatedAt) = ReadTimeline(session.GetTimelineProperties());
        var controls = playback.Controls;

        return new MediaSnapshot(
            appId,
            AppDisplayNameResolver.Resolve(appId),
            properties.Title,
            properties.Artist ?? string.Empty,
            properties.AlbumTitle ?? string.Empty,
            entry.Thumbnail,
            MapStatus(playback.PlaybackStatus),
            position,
            duration,
            updatedAt,
            controls.IsPlayEnabled,
            controls.IsPauseEnabled,
            controls.IsNextEnabled,
            controls.IsPreviousEnabled)
        {
            SessionId = entry.Id,
            Kind = MapKind(playback.PlaybackType ?? properties.PlaybackType),
        };
    }

    private (TimeSpan? Position, TimeSpan? Duration, DateTimeOffset UpdatedAt) ReadTimeline(
        GlobalSystemMediaTransportControlsSessionTimelineProperties? timeline)
    {
        var now = time.GetUtcNow();
        if (timeline is null || timeline.EndTime <= timeline.StartTime)
        {
            return (null, null, now);
        }

        // Alguns aplicativos não preenchem LastUpdatedTime; nesse caso a posição é considerada atual.
        var updatedAt = timeline.LastUpdatedTime;
        if (updatedAt > now || now - updatedAt > TimeSpan.FromDays(1))
        {
            updatedAt = now;
        }

        var duration = timeline.EndTime - timeline.StartTime;
        var position = timeline.Position - timeline.StartTime;
        return (position < TimeSpan.Zero ? TimeSpan.Zero : position, duration, updatedAt);
    }

    private async Task<byte[]?> TryReadThumbnailAsync(global::Windows.Storage.Streams.IRandomAccessStreamReference? thumbnail)
    {
        try
        {
            return await WinRtStreams.ReadAllBytesAsync(thumbnail);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Capa da mídia indisponível.");
            return null;
        }
    }

    private static MediaPlaybackState MapStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus status) => status switch
    {
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlaybackState.Playing,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlaybackState.Paused,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => MediaPlaybackState.Changing,
        _ => MediaPlaybackState.Stopped,
    };

    private static MediaKind MapKind(MediaPlaybackType? type) => type switch
    {
        MediaPlaybackType.Music => MediaKind.Music,
        MediaPlaybackType.Video => MediaKind.Video,
        _ => MediaKind.Unknown,
    };

    private void Publish(IReadOnlyList<MediaSnapshot> snapshots, string? currentId)
    {
        lock (_lock)
        {
            if (currentId == CurrentSessionId && snapshots.SequenceEqual(_sessions))
            {
                return;
            }

            _sessions = snapshots;
            CurrentSessionId = currentId;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Tracked(string id, string appId, Session session)
    {
        public string Id { get; } = id;

        public string AppId { get; } = appId;

        public Session Session { get; } = session;

        public string? ThumbnailTrackKey { get; set; }

        public byte[]? Thumbnail { get; set; }
    }
}
