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
    IIslandSettingsService settings) : IIslandModule
{
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
                    state = new SessionState(new MediaContent(snapshot.Id, media, ui, time, CanAnimateIndicator), now);
                    _sessions[snapshot.Id] = state;
                }

                if (snapshot.IsPlaying)
                {
                    state.LastPlayingAt = now;
                }

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
    }
}
