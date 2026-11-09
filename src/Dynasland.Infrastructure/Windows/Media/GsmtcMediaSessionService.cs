using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.Media.Control;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using SessionManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace Dynasland.Infrastructure.Windows.Media;

/// <summary>
/// Sessão de mídia atual do Windows via Global System Media Transport Controls.
/// Totalmente orientado a eventos: a sessão notifica mudanças de faixa, estado e linha do tempo.
/// </summary>
public sealed class GsmtcMediaSessionService(TimeProvider time, ILogger<GsmtcMediaSessionService> logger) : IMediaSessionService
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Lock _lock = new();

    private SessionManager? _manager;
    private Session? _session;
    private string? _thumbnailTrackKey;
    private byte[]? _thumbnail;

    public MediaSnapshot? Current { get; private set; }

    public event EventHandler<MediaSnapshot?>? Changed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var manager = await SessionManager.RequestAsync().AsTask(cancellationToken);
        lock (_lock)
        {
            _manager = manager;
            manager.CurrentSessionChanged += OnCurrentSessionChanged;
            AttachSession(manager.GetCurrentSession());
        }

        await RefreshAsync();
    }

    public Task StopAsync()
    {
        lock (_lock)
        {
            if (_manager is not null)
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                _manager = null;
            }

            AttachSession(null);
        }

        Current = null;
        return Task.CompletedTask;
    }

    public Task<bool> PlayAsync() => Control(s => s.TryPlayAsync());

    public Task<bool> PauseAsync() => Control(s => s.TryPauseAsync());

    public Task<bool> SkipNextAsync() => Control(s => s.TrySkipNextAsync());

    public Task<bool> SkipPreviousAsync() => Control(s => s.TrySkipPreviousAsync());

    private async Task<bool> Control(Func<Session, global::Windows.Foundation.IAsyncOperation<bool>> command)
    {
        Session? session;
        lock (_lock)
        {
            session = _session;
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
            logger.LogWarning(ex, "Comando de mídia recusado por {App}.", session.SourceAppUserModelId);
            return false;
        }
    }

    private void OnCurrentSessionChanged(SessionManager sender, CurrentSessionChangedEventArgs args)
    {
        lock (_lock)
        {
            AttachSession(sender.GetCurrentSession());
        }

        _ = RefreshAsync();
    }

    /// <summary>Troca a sessão observada. Chamar com <see cref="_lock"/> adquirido.</summary>
    private void AttachSession(Session? session)
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionChanged;
            _session.PlaybackInfoChanged -= OnSessionChanged;
            _session.TimelinePropertiesChanged -= OnSessionChanged;
        }

        _session = session;

        if (session is not null)
        {
            session.MediaPropertiesChanged += OnSessionChanged;
            session.PlaybackInfoChanged += OnSessionChanged;
            session.TimelinePropertiesChanged += OnSessionChanged;
        }
    }

    private void OnSessionChanged(Session sender, object args) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        await _refreshGate.WaitAsync();
        try
        {
            Session? session;
            lock (_lock)
            {
                session = _session;
            }

            Publish(session is null ? null : await ReadAsync(session));
        }
        catch (Exception ex)
        {
            // Sessões encerradas durante a leitura lançam COMException; a próxima notificação corrige o estado.
            logger.LogDebug(ex, "Falha ao ler a sessão de mídia.");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<MediaSnapshot?> ReadAsync(Session session)
    {
        var properties = await session.TryGetMediaPropertiesAsync();
        if (properties is null || string.IsNullOrWhiteSpace(properties.Title))
        {
            return null;
        }

        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var appId = session.SourceAppUserModelId ?? string.Empty;

        var trackKey = $"{appId}\u001f{properties.Title}\u001f{properties.Artist}\u001f{properties.AlbumTitle}";
        if (trackKey != _thumbnailTrackKey)
        {
            _thumbnailTrackKey = trackKey;
            _thumbnail = await TryReadThumbnailAsync(properties.Thumbnail);
        }

        var (position, duration, updatedAt) = ReadTimeline(timeline);
        var controls = playback.Controls;

        return new MediaSnapshot(
            appId,
            AppDisplayNameResolver.Resolve(appId),
            properties.Title,
            properties.Artist ?? string.Empty,
            properties.AlbumTitle ?? string.Empty,
            _thumbnail,
            MapStatus(playback.PlaybackStatus),
            position,
            duration,
            updatedAt,
            controls.IsPlayEnabled,
            controls.IsPauseEnabled,
            controls.IsNextEnabled,
            controls.IsPreviousEnabled);
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

    private void Publish(MediaSnapshot? snapshot)
    {
        if (snapshot == Current)
        {
            return;
        }

        Current = snapshot;
        Changed?.Invoke(this, snapshot);
    }
}
