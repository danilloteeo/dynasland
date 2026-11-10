using System.Windows.Media;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Media;

/// <summary>
/// Mídias do Windows (Spotify, navegadores...). Cada sessão vira uma atividade contínua própria —
/// uma aba no estado interativo — e o compacto mostra a mais relevante: a sessão atual do Windows
/// tocando, depois outras tocando, depois as pausadas. Mídias pausadas há muito tempo saem da ilha.
/// </summary>
public sealed class MediaModule(
    IMediaSessionService media,
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IModuleOptions options,
    IIslandSettingsService settings,
    ISpotifyService? spotify = null,
    IAudioVolumeService? volume = null) : IIslandModule
{
    public const string QueueActivityId = "media.queue";
    private const int FullQueueSize = 20;
    private static readonly TimeSpan SkipStepDelay = TimeSpan.FromMilliseconds(350);

    private const int QueueImageWidth = 56;

    public const string ModuleId = "media";
    public const string SessionActivityPrefix = "media.session.";
    public const string TrackActivityId = "media.track";

    private static readonly TimeSpan TrackAnnouncementDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan StaleCheckInterval = TimeSpan.FromSeconds(30);
    private const int ArtworkDecodeWidth = 160;

    private static readonly string[] BrowserHints =
        ["chrome", "msedge", "edge", "opera", "firefox", "308046B0AF4A39CB", "brave", "vivaldi", "navegador", "browser"];

    private readonly Lock _lock = new();
    private readonly Dictionary<string, SessionState> _sessions = new(StringComparer.Ordinal);
    private ITimer? _staleTimer;
    private bool _running;

    public static readonly ModuleOption AnnounceTracks = ModuleOption.Toggle(
        "announceTracks", "Anunciar nova música", "Expande a ilha quando uma nova faixa começa a tocar.");

    public static readonly ModuleOption AnnounceVideos = ModuleOption.Toggle(
        "announceVideos", "Anunciar também vídeos", "Inclui vídeos e filmes (ex.: no navegador) nos anúncios. Desligado, não interrompe quem está assistindo.", defaultValue: false);

    public static readonly ModuleOption AnimatedEqualizer = ModuleOption.Toggle(
        "animatedEqualizer", "Equalizador animado", "Barras animadas na ilha compacta enquanto toca (desligado, ficam paradas).");

    public static readonly ModuleOption PausedTimeout = ModuleOption.Slider(
        "pausedTimeoutMinutes", "Remover mídia pausada após", "Uma mídia pausada sai da ilha depois desse tempo e volta quando tocar de novo.", 10, 1, 60, 1, "min");

    public string Id => ModuleId;

    public string DisplayName => "Música e mídia";

    public string Description => "Música e vídeos em reprodução (Spotify, navegadores e outros apps). Várias mídias aparecem como abas.";

    public IReadOnlyList<ModuleOption> Options => [AnnounceTracks, AnnounceVideos, AnimatedEqualizer, PausedTimeout];

    public static string SessionActivityId(string sessionId) => SessionActivityPrefix + sessionId;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _sessions.Clear();
            _running = true;
        }

        // A primeira leitura (feita no StartAsync) não é "mudança": o que já estava tocando
        // quando o Dynasland abriu vira indicador, sem anúncio. Só depois passamos a ouvir eventos.
        await media.StartAsync(cancellationToken);
        media.Changed += OnMediaChanged;
        options.Changed += OnOptionsChanged;
        settings.Changed += OnSettingsChanged;
        Apply(announce: false);

        _staleTimer = time.CreateTimer(_ => Apply(announce: true), null, StaleCheckInterval, StaleCheckInterval);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        media.Changed -= OnMediaChanged;
        options.Changed -= OnOptionsChanged;
        settings.Changed -= OnSettingsChanged;
        _staleTimer?.Dispose();
        _staleTimer = null;
        await media.StopAsync();

        lock (_lock)
        {
            _running = false;
            foreach (var state in _sessions.Values)
            {
                var content = state.Content;
                ui.Post(content.Dispose);
            }

            _sessions.Clear();
        }
    }

    /// <summary>
    /// Vídeo para fins de anúncio. Navegadores não são confiáveis no tipo informado (o Opera informa
    /// "música" para filmes): no navegador, tipo desconhecido ou mídia sem artista conta como vídeo,
    /// enquanto música com artista (ex.: YouTube Music, Spotify Web) continua sendo música.
    /// </summary>
    public static bool IsLikelyVideo(MediaSnapshot snapshot)
    {
        if (snapshot.Kind == MediaKind.Video)
        {
            return true;
        }

        var isBrowser = BrowserHints.Any(hint =>
            snapshot.SourceAppId.Contains(hint, StringComparison.OrdinalIgnoreCase)
            || snapshot.AppName.Contains(hint, StringComparison.OrdinalIgnoreCase));

        return isBrowser && (snapshot.Kind == MediaKind.Unknown || string.IsNullOrWhiteSpace(snapshot.Artist));
    }

    private void OnMediaChanged(object? sender, EventArgs e) => Apply(announce: true);

    private void OnOptionsChanged(object? sender, (string ModuleId, string Key) e)
    {
        if (e.ModuleId == ModuleId)
        {
            RefreshAnimation();
            Apply(announce: true);
        }
    }

    private void OnSettingsChanged(object? sender, IslandSettings e) => RefreshAnimation();

    private bool CanAnimateIndicator() => settings.Current.AnimationsEnabled && options.IsOn(ModuleId, AnimatedEqualizer);

    private void RefreshAnimation()
    {
        List<MediaContent> contents;
        lock (_lock)
        {
            contents = [.. _sessions.Values.Select(s => s.Content)];
        }

        foreach (var content in contents)
        {
            ui.Post(content.RefreshAnimation);
        }
    }

    private void Apply(bool announce)
    {
        var snapshots = media.Sessions;
        var currentId = media.CurrentSessionId;
        var now = time.GetUtcNow();
        var pausedTimeout = TimeSpan.FromMinutes(options.Get(ModuleId, PausedTimeout));

        lock (_lock)
        {
            if (!_running)
            {
                return;
            }

            // Sessões encerradas (aplicativo fechado, aba fechada).
            foreach (var goneId in _sessions.Keys.Where(id => snapshots.All(s => s.Id != id)).ToList())
            {
                var gone = _sessions[goneId];
                if (gone.IsPosted)
                {
                    bus.RemoveActivity(SessionActivityId(goneId));
                }

                ui.Post(gone.Content.Dispose);
                _sessions.Remove(goneId);
            }

            foreach (var snapshot in snapshots)
            {
                if (!_sessions.TryGetValue(snapshot.Id, out var state))
                {
                    var sessionId = snapshot.Id;
                    var actions = new MediaPanelActions(
                        () =>
                        {
                            _ = RefreshQueueAsync(sessionId);
                            RefreshVolume(sessionId);
                        },
                        () => _ = ShowFullQueueAsync(sessionId),
                        level => volume?.SetLevel((float)level));
                    state = new SessionState(new MediaContent(snapshot.Id, media, ui, time, CanAnimateIndicator, actions), now);
                    _sessions[snapshot.Id] = state;
                }

                if (snapshot.IsPlaying)
                {
                    state.LastPlayingAt = now;
                }

                state.IsSpotify = IsSpotify(snapshot);

                if (!ReferenceEquals(snapshot.Thumbnail, state.ArtworkBytes))
                {
                    state.ArtworkBytes = snapshot.Thumbnail;
                    state.Artwork = ImageDecoder.Decode(snapshot.Thumbnail, ArtworkDecodeWidth);
                }

                var content = state.Content;
                var artwork = state.Artwork;
                var others = snapshots.Count - 1;
                ui.Post(() => content.Update(snapshot, artwork, others));

                UpdateActivity(snapshot, state, currentId, now - state.LastPlayingAt >= pausedTimeout);
                UpdateAnnouncement(snapshot, state, announce);

                // Faixa nova no Spotify: a fila mudou (lê já, para o painel abrir com ela pronta).
                if (snapshot.TrackKey != state.QueueTrackKey && IsSpotify(snapshot))
                {
                    state.QueueTrackKey = snapshot.TrackKey;
                    _ = RefreshQueueAsync(snapshot.Id);
                }
            }
        }
    }

    /// <summary>Publica, reprioriza ou retira a aba da sessão. Chamar com <see cref="_lock"/>.</summary>
    private void UpdateActivity(MediaSnapshot snapshot, SessionState state, string? currentId, bool stale)
    {
        var activityId = SessionActivityId(snapshot.Id);
        if (stale)
        {
            if (state.IsPosted)
            {
                bus.RemoveActivity(activityId);
                state.IsPosted = false;
            }

            return;
        }

        // A ordem das abas e o indicador compacto seguem a prioridade.
        var priority = snapshot.IsPlaying
            ? snapshot.Id == currentId ? ActivityPriority.High : ActivityPriority.Normal
            : ActivityPriority.Low;

        if (!state.IsPosted || state.Priority != priority)
        {
            bus.PostActivity(new IslandActivity(activityId, ModuleId, ActivityKind.Live, state.Content) { Priority = priority });
            state.IsPosted = true;
            state.Priority = priority;
        }
    }

    /// <summary>Anuncia cada faixa uma vez, quando começa a tocar. Chamar com <see cref="_lock"/>.</summary>
    private void UpdateAnnouncement(MediaSnapshot snapshot, SessionState state, bool announce)
    {
        if (snapshot.IsPlaying && snapshot.TrackKey != state.AnnouncedTrackKey)
        {
            if (announce && ShouldAnnounce(snapshot))
            {
                bus.PostActivity(new IslandActivity(TrackActivityId, ModuleId, ActivityKind.Transient, state.Content)
                {
                    Duration = TrackAnnouncementDuration,
                });
            }

            state.AnnouncedTrackKey = snapshot.TrackKey;
        }
        else if (!announce && state.AnnouncedTrackKey is null)
        {
            // Na inicialização, o que já estava carregado (mesmo pausado) não é anunciado ao retomar.
            state.AnnouncedTrackKey = snapshot.TrackKey;
        }
    }

    public static bool IsSpotify(MediaSnapshot snapshot) =>
        snapshot.SourceAppId.Contains("Spotify", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// "A seguir" pela API Web do Spotify (só com a conta conectada nas configurações). Sem conexão ou sem
    /// resposta, a lista fica vazia e o painel continua igual.
    /// </summary>
    private async Task RefreshQueueAsync(string sessionId)
    {
        MediaContent content;
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var state) || !state.IsSpotify)
            {
                return;
            }

            content = state.Content;
        }

        if (spotify is not { State: SpotifyConnectionState.Connected })
        {
            ui.Post(() => content.SetQueue([]));
            return;
        }

        var queue = await spotify.GetQueueAsync();
        var items = await BuildQueueItemsAsync(sessionId, queue, MediaContent.QueueSize);
        var total = queue?.Upcoming.Count ?? 0;

        ui.Post(() =>
        {
            var resized = content.Queue.Count != items.Count;
            content.SetQueue(items);
            content.QueueTotal = total;
            if (resized)
            {
                Repost(sessionId);
            }
        });
    }

    private async Task<List<QueueItemViewModel>> BuildQueueItemsAsync(string sessionId, SpotifyQueue? queue, int count)
    {
        var items = new List<QueueItemViewModel>();
        var position = 0;
        foreach (var track in queue?.Upcoming.Take(count) ?? [])
        {
            var steps = ++position;
            var image = track.ImageUrl is { } url && spotify is not null ? ImageDecoder.Decode(await spotify.GetImageAsync(url), QueueImageWidth) : null;
            items.Add(new QueueItemViewModel(steps, track.Name, track.Artists, image, new CommunityToolkit.Mvvm.Input.RelayCommand(() => _ = SkipAheadAsync(sessionId, steps))));
        }

        return items;
    }

    /// <summary>
    /// Pula até a faixa escolhida da fila avançando "próxima" pelos controles de mídia do Windows (funciona
    /// em contas gratuitas; a API do Spotify exigiria Premium para isso).
    /// </summary>
    private async Task SkipAheadAsync(string sessionId, int steps)
    {
        bus.RemoveActivity(QueueActivityId);
        for (var i = 0; i < steps; i++)
        {
            if (!await media.SkipNextAsync(sessionId))
            {
                break;
            }

            if (i < steps - 1)
            {
                await Task.Delay(SkipStepDelay, time);
            }
        }
    }

    /// <summary>Volume geral do sistema no painel (o mesmo das teclas de volume).</summary>
    private void RefreshVolume(string sessionId)
    {
        if (volume?.Current is not { } current)
        {
            return;
        }

        MediaContent content;
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var state))
            {
                return;
            }

            content = state.Content;
        }

        ui.Post(() =>
        {
            var resized = !content.HasAppVolume;
            content.ApplyAppVolume(current.IsMuted ? 0 : current.Level);
            if (resized)
            {
                Repost(sessionId);
            }
        });
    }

    /// <summary>Abre a fila completa (até 20 faixas) como um painel próprio na ilha.</summary>
    private async Task ShowFullQueueAsync(string sessionId)
    {
        if (spotify is not { State: SpotifyConnectionState.Connected })
        {
            return;
        }

        var queue = await spotify.GetQueueAsync();
        var items = await BuildQueueItemsAsync(sessionId, queue, FullQueueSize);
        var content = new QueueContent(items.Count == 0 ? "A fila está vazia" : "Fila do Spotify · clique para pular até a faixa");
        ui.Post(() =>
        {
            foreach (var item in items)
            {
                content.Items.Add(item);
            }

            bus.PostActivity(new IslandActivity(QueueActivityId, ModuleId, ActivityKind.Transient, content) { OpenInteractive = true });
        });
    }

    /// <summary>Republica a aba para a ilha recalcular o tamanho (a fila mudou de tamanho).</summary>
    private void Repost(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var state) && state.IsPosted)
            {
                bus.PostActivity(new IslandActivity(SessionActivityId(sessionId), ModuleId, ActivityKind.Live, state.Content) { Priority = state.Priority });
            }
        }
    }

    private bool ShouldAnnounce(MediaSnapshot snapshot) =>
        options.IsOn(ModuleId, AnnounceTracks) && (!IsLikelyVideo(snapshot) || options.IsOn(ModuleId, AnnounceVideos));

    private sealed class SessionState(MediaContent content, DateTimeOffset createdAt)
    {
        public MediaContent Content { get; } = content;

        public DateTimeOffset LastPlayingAt { get; set; } = createdAt;

        public bool IsPosted { get; set; }

        public ActivityPriority Priority { get; set; }

        public string? AnnouncedTrackKey { get; set; }

        public byte[]? ArtworkBytes { get; set; }

        public ImageSource? Artwork { get; set; }

        public bool IsSpotify { get; set; }

        /// <summary>Faixa para a qual a fila do Spotify foi lida por último.</summary>
        public string? QueueTrackKey { get; set; }
    }
}
