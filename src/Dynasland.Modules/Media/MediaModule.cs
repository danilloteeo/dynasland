using System.Windows.Media;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Media;

/// <summary>
/// Mídia do Windows (Spotify, navegadores...): atividade contínua enquanto houver sessão e
/// anúncio expandido quando uma nova faixa começa a tocar.
/// </summary>
public sealed class MediaModule(IMediaSessionService media, IEventBus bus, IUiDispatcher ui, TimeProvider time, IModuleOptions options, IIslandSettingsService settings) : IIslandModule
{
    public const string ModuleId = "media";
    public const string SessionActivityId = "media.session";
    public const string TrackActivityId = "media.track";

    private static readonly TimeSpan TrackAnnouncementDuration = TimeSpan.FromSeconds(4);
    private const int ArtworkDecodeWidth = 160;

    private readonly Lock _lock = new();
    private MediaContent? _content;
    private bool _sessionPosted;
    private string? _announcedTrackKey;
    private byte[]? _artworkBytes;
    private ImageSource? _artwork;

    public string Id => ModuleId;

    public string DisplayName => "Música e mídia";

    public string Description => "Música e vídeos em reprodução (Spotify, navegadores e outros apps).";

    public static readonly ModuleOption AnnounceTracks = ModuleOption.Toggle(
        "announceTracks", "Anunciar nova música", "Expande a ilha quando uma nova faixa começa a tocar.");

    public static readonly ModuleOption AnimatedEqualizer = ModuleOption.Toggle(
        "animatedEqualizer", "Equalizador animado", "Barras animadas na ilha compacta enquanto toca (desligado, ficam paradas).");

    public IReadOnlyList<ModuleOption> Options => [AnnounceTracks, AnimatedEqualizer];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _content = new MediaContent(media, ui, time, CanAnimateIndicator);
            _sessionPosted = false;
            _announcedTrackKey = null;
        }

        // A primeira leitura (feita no StartAsync) não é "mudança": o que já estava tocando
        // quando o Dynasland abriu vira indicador, sem anúncio. Só depois passamos a ouvir eventos.
        await media.StartAsync(cancellationToken);
        media.Changed += OnMediaChanged;
        options.Changed += OnPreferencesChanged;
        settings.Changed += OnPreferencesChanged;
        Apply(media.Current, announce: false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        media.Changed -= OnMediaChanged;
        options.Changed -= OnPreferencesChanged;
        settings.Changed -= OnPreferencesChanged;
        await media.StopAsync();

        lock (_lock)
        {
            var content = _content;
            _content = null;
            if (content is not null)
            {
                ui.Post(content.Dispose);
            }
        }
    }

    private void OnMediaChanged(object? sender, MediaSnapshot? snapshot) => Apply(snapshot, announce: true);

    private bool CanAnimateIndicator() => settings.Current.AnimationsEnabled && options.IsOn(ModuleId, AnimatedEqualizer);

    private void OnPreferencesChanged(object? sender, (string ModuleId, string Key) e) => RefreshAnimation();

    private void OnPreferencesChanged(object? sender, IslandSettings e) => RefreshAnimation();

    private void RefreshAnimation()
    {
        if (_content is { } content)
        {
            ui.Post(content.RefreshAnimation);
        }
    }

    private void Apply(MediaSnapshot? snapshot, bool announce)
    {
        lock (_lock)
        {
            if (_content is not { } content)
            {
                return;
            }

            if (snapshot is null)
            {
                if (_sessionPosted)
                {
                    bus.RemoveActivity(SessionActivityId);
                    bus.RemoveActivity(TrackActivityId);
                    _sessionPosted = false;
                }

                _announcedTrackKey = null;
                return;
            }

            if (!ReferenceEquals(snapshot.Thumbnail, _artworkBytes))
            {
                _artworkBytes = snapshot.Thumbnail;
                _artwork = ImageDecoder.Decode(snapshot.Thumbnail, ArtworkDecodeWidth);
            }

            var artwork = _artwork;
            ui.Post(() => content.Update(snapshot, artwork));

            if (!_sessionPosted)
            {
                bus.PostActivity(new IslandActivity(SessionActivityId, ModuleId, ActivityKind.Live, content));
                _sessionPosted = true;
            }

            // Anuncia cada faixa uma vez, quando começa a tocar (troca durante a pausa é anunciada ao retomar).
            if (snapshot.IsPlaying && snapshot.TrackKey != _announcedTrackKey)
            {
                if (announce && options.IsOn(ModuleId, AnnounceTracks))
                {
                    bus.PostActivity(new IslandActivity(TrackActivityId, ModuleId, ActivityKind.Transient, content)
                    {
                        Duration = TrackAnnouncementDuration,
                    });
                }

                _announcedTrackKey = snapshot.TrackKey;
            }
            else if (!announce)
            {
                _announcedTrackKey = snapshot.TrackKey;
            }
        }
    }
}
