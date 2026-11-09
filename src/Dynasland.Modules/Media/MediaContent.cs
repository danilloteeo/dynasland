using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Media;

/// <summary>View model da mídia em reprodução. Atualizado sempre na thread de UI.</summary>
public sealed partial class MediaContent(IMediaSessionService media, IUiDispatcher ui, TimeProvider time, Func<bool> canAnimateIndicator) : ObservableObject, IActivityContent, IDisposable
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(1);

    private MediaSnapshot? _snapshot;
    private IslandState? _presentedIn;
    private ITimer? _progressTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private string _artist = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private string _appName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArtwork))]
    private ImageSource? _artwork;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseGlyph))]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _canPlayPause;

    [ObservableProperty]
    private bool _canSkipNext;

    [ObservableProperty]
    private bool _canSkipPrevious;

    [ObservableProperty]
    private bool _hasTimeline;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _positionText = string.Empty;

    [ObservableProperty]
    private string _durationText = string.Empty;

    /// <summary>Equalizador animado: só enquanto toca e o indicador compacto está visível.</summary>
    [ObservableProperty]
    private bool _isIndicatorAnimating;

    public bool HasArtwork => Artwork is not null;

    public string Subtitle => string.IsNullOrWhiteSpace(Artist) ? AppName : Artist;

    public string PlayPauseGlyph => IsPlaying ? Glyphs.Pause : Glyphs.Play;

    public string MusicGlyph => Glyphs.MusicNote;

    public string PreviousGlyph => Glyphs.Previous;

    public string NextGlyph => Glyphs.Next;

    public void Update(MediaSnapshot snapshot, ImageSource? artwork)
    {
        _snapshot = snapshot;
        Title = snapshot.Title;
        Artist = snapshot.Artist;
        AppName = snapshot.AppName;
        Artwork = artwork;
        IsPlaying = snapshot.IsPlaying;
        CanPlayPause = snapshot.IsPlaying ? snapshot.CanPause : snapshot.CanPlay;
        CanSkipNext = snapshot.CanSkipNext;
        CanSkipPrevious = snapshot.CanSkipPrevious;
        HasTimeline = snapshot.Duration is { } duration && duration > TimeSpan.Zero;
        RefreshProgress();
        UpdateTimers();
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Compact => new IslandDimensions(232, 34, 17),
        IslandState.Expanded => new IslandDimensions(360, 72, 30),
        IslandState.Interactive => new IslandDimensions(400, 168, 36),
        _ => null,
    };

    public void OnPresentationChanged(IslandState? state)
    {
        _presentedIn = state;
        RefreshProgress();
        UpdateTimers();
    }

    [RelayCommand]
    private Task PlayPauseAsync() => IsPlaying ? media.PauseAsync() : media.PlayAsync();

    [RelayCommand]
    private Task NextAsync() => media.SkipNextAsync();

    [RelayCommand]
    private Task PreviousAsync() => media.SkipPreviousAsync();

    /// <summary>Reavalia o equalizador após mudança nas preferências de animação.</summary>
    public void RefreshAnimation() => UpdateTimers();

    private void UpdateTimers()
    {
        IsIndicatorAnimating = IsPlaying && _presentedIn == IslandState.Compact && canAnimateIndicator();

        // A barra de progresso só avança enquanto alguém a vê.
        var needsProgress = IsPlaying && HasTimeline && _presentedIn is IslandState.Expanded or IslandState.Interactive;
        if (needsProgress && _progressTimer is null)
        {
            _progressTimer = time.CreateTimer(_ => ui.Post(RefreshProgress), null, ProgressInterval, ProgressInterval);
        }
        else if (!needsProgress && _progressTimer is not null)
        {
            _progressTimer.Dispose();
            _progressTimer = null;
        }
    }

    private void RefreshProgress()
    {
        if (_snapshot?.Duration is { } duration && duration > TimeSpan.Zero
            && _snapshot.EstimatePosition(time.GetUtcNow()) is { } position)
        {
            Progress = position / duration;
            PositionText = TextFormat.MediaTime(position);
            DurationText = TextFormat.MediaTime(duration);
        }
        else
        {
            Progress = 0;
            PositionText = string.Empty;
            DurationText = string.Empty;
        }
    }

    public void Dispose()
    {
        _progressTimer?.Dispose();
        _progressTimer = null;
    }
}
