using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Core.Services;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Media;

/// <summary>Faixa da fila do Spotify; <see cref="Select"/> pula até ela.</summary>
public sealed record QueueItemViewModel(int Position, string Name, string Artists, ImageSource? Image, IRelayCommand Select)
{
    public bool HasImage => Image is not null;
}

/// <summary>Ações extras do painel de mídia, fornecidas pelo módulo.</summary>
/// <param name="RefreshExtras">Painel aberto: atualizar fila e volume do app.</param>
public sealed record MediaPanelActions(Action RefreshExtras, Action ShowFullQueue, Action<double> SetAppVolume);

/// <summary>Fila completa do Spotify (aberta por "Ver fila completa").</summary>
public sealed class QueueContent(string title) : IActivityContent
{
    public string Title { get; } = title;

    public System.Collections.ObjectModel.ObservableCollection<QueueItemViewModel> Items { get; } = [];

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state == IslandState.Interactive ? new IslandDimensions(400, Math.Min(380, 64 + Items.Count * 40), 34) : null;
}

/// <summary>View model de uma sessão de mídia. Atualizado sempre na thread de UI.</summary>
public sealed partial class MediaContent(string sessionId, IMediaSessionService media, IUiDispatcher ui, TimeProvider time, Func<bool> canAnimateIndicator, MediaPanelActions? actions = null) : ObservableObject, IActivityContent, IDisposable
{
    private bool _applyingVolume;

    /// <summary>Volume do sistema (0 a 100), para ajustar sem sair do painel de mídia.</summary>
    [ObservableProperty]
    private double _appVolume;

    [ObservableProperty]
    private bool _hasAppVolume;

    /// <summary>De onde vem o volume do controle ("Volume do Spotify" ou "Volume do sistema").</summary>
    [ObservableProperty]
    private string _volumeLabel = "Volume do sistema";

    /// <summary>Aviso abaixo do volume (ex.: volume do Spotify zerado, falta de permissão).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVolumeMessage))]
    private string? _volumeMessage;

    public bool HasVolumeMessage => !string.IsNullOrEmpty(VolumeMessage);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullQueueText), nameof(HasMoreQueue))]
    private int _queueTotal;

    public string FullQueueText => $"Ver fila completa ({QueueTotal})";

    public bool HasMoreQueue => QueueTotal > 0;

    public string VolumeGlyph => Glyphs.VolumeMedium;

    /// <summary>Reflete o volume atual sem reenviá-lo. Chamar na thread de UI.</summary>
    public void ApplyAppVolume(float? level)
    {
        _applyingVolume = true;
        try
        {
            HasAppVolume = level is not null;
            if (level is { } value)
            {
                AppVolume = Math.Round(value * 100);
            }
        }
        finally
        {
            _applyingVolume = false;
        }
    }

    partial void OnAppVolumeChanged(double value)
    {
        if (!_applyingVolume)
        {
            actions?.SetAppVolume(value / 100);
        }
    }

    [RelayCommand]
    private void ShowFullQueue() => actions?.ShowFullQueue();

    /// <summary>Máximo de faixas "a seguir" exibidas no painel.</summary>
    public const int QueueSize = 3;

    public System.Collections.ObjectModel.ObservableCollection<QueueItemViewModel> Queue { get; } = [];

    public bool HasQueue => Queue.Count > 0;

    /// <summary>Atualiza "A seguir". Chamar na thread de UI.</summary>
    public void SetQueue(IReadOnlyList<QueueItemViewModel> items)
    {
        Queue.Clear();
        foreach (var item in items.Take(QueueSize))
        {
            Queue.Add(item);
        }

        OnPropertyChanged(nameof(HasQueue));
    }

    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(1);

    // A letra troca de verso com mais precisão que a barra de progresso.
    private static readonly TimeSpan LyricsInterval = TimeSpan.FromMilliseconds(250);

    private IReadOnlyList<LyricLine> _lyrics = [];
    private int _lyricIndex = -2;
    private ITimer? _lyricsTimer;

    /// <summary>Há letra sincronizada para a faixa atual.</summary>
    public bool HasLyrics => _lyrics.Count > 0;

    [ObservableProperty]
    private string _lyricPrevious = string.Empty;

    [ObservableProperty]
    private string _lyricCurrent = string.Empty;

    [ObservableProperty]
    private string _lyricNext = string.Empty;

    /// <summary>Define a letra da faixa (ou nenhuma). Ignorada se a faixa já mudou. Chamar na thread de UI.</summary>
    /// <returns><c>true</c> se o painel mudou de tamanho (passou a ter ou deixou de ter letra).</returns>
    public bool SetLyrics(string trackKey, IReadOnlyList<LyricLine>? lines)
    {
        if (_snapshot is null || _snapshot.TrackKey != trackKey)
        {
            return false;
        }

        var had = HasLyrics;
        _lyrics = lines ?? [];
        _lyricIndex = -2;
        OnPropertyChanged(nameof(HasLyrics));
        RefreshLyrics();
        UpdateTimers();
        return had != HasLyrics;
    }

    /// <summary>Tira a letra (a faixa mudou ou a opção foi desligada). Chamar na thread de UI.</summary>
    /// <returns><c>true</c> se havia letra e o painel encolheu.</returns>
    public bool ClearLyrics()
    {
        if (!HasLyrics)
        {
            return false;
        }

        _lyrics = [];
        OnPropertyChanged(nameof(HasLyrics));
        UpdateTimers();
        return true;
    }

    private void RefreshLyrics()
    {
        if (!HasLyrics || _snapshot is null)
        {
            return;
        }

        var index = LrcParser.IndexAt(_lyrics, _snapshot.EstimatePosition(time.GetUtcNow()) ?? TimeSpan.Zero);
        if (index == _lyricIndex)
        {
            return;
        }

        _lyricIndex = index;
        LyricPrevious = LineAt(index - 1);
        LyricCurrent = index < 0 ? "♪" : LineAt(index);
        LyricNext = LineAt(index + 1);
    }

    /// <summary>Versos vazios no arquivo marcam trechos instrumentais.</summary>
    private string LineAt(int index) =>
        index < 0 || index >= _lyrics.Count ? string.Empty : _lyrics[index].Text.Length == 0 ? "♪" : _lyrics[index].Text;

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

    /// <summary>Quantas outras mídias existem (exibido como "+N" no compacto).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOtherSessions), nameof(OtherSessionsText))]
    private int _otherSessions;

    public string SessionId => sessionId;

    public bool HasOtherSessions => OtherSessions > 0;

    public string OtherSessionsText => $"+{OtherSessions}";

    public bool HasArtwork => Artwork is not null;

    public string Subtitle => string.IsNullOrWhiteSpace(Artist) ? AppName : Artist;

    public string PlayPauseGlyph => IsPlaying ? Glyphs.Pause : Glyphs.Play;

    public string MusicGlyph => Glyphs.MusicNote;

    public string PreviousGlyph => Glyphs.Previous;

    public string NextGlyph => Glyphs.Next;

    public void Update(MediaSnapshot snapshot, ImageSource? artwork, int otherSessions = 0)
    {
        _snapshot = snapshot;
        OtherSessions = otherSessions;
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
        IslandState.Compact => new IslandDimensions(HasOtherSessions ? 252 : 232, 34, 17),
        IslandState.Expanded => new IslandDimensions(360, 72, 30),
        IslandState.Interactive => new IslandDimensions(400, 168 + (HasAppVolume ? 34 : 0) + (HasVolumeMessage ? 18 : 0) + (HasQueue ? 30 + Queue.Count * 34 : 0) + (HasLyrics ? 70 : 0), 36),
        _ => null,
    };

    public void OnPresentationChanged(IslandState? state)
    {
        if (state == IslandState.Interactive && _presentedIn != IslandState.Interactive)
        {
            actions?.RefreshExtras();
        }

        _presentedIn = state;
        RefreshProgress();
        UpdateTimers();
    }

    [RelayCommand]
    private Task PlayPauseAsync() => IsPlaying ? media.PauseAsync(sessionId) : media.PlayAsync(sessionId);

    [RelayCommand]
    private Task NextAsync() => media.SkipNextAsync(sessionId);

    [RelayCommand]
    private Task PreviousAsync() => media.SkipPreviousAsync(sessionId);

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

        var needsLyrics = IsPlaying && HasLyrics && _presentedIn == IslandState.Interactive;
        if (needsLyrics && _lyricsTimer is null)
        {
            _lyricsTimer = time.CreateTimer(_ => ui.Post(RefreshLyrics), null, LyricsInterval, LyricsInterval);
        }
        else if (!needsLyrics && _lyricsTimer is not null)
        {
            _lyricsTimer.Dispose();
            _lyricsTimer = null;
        }
    }

    private void RefreshProgress()
    {
        RefreshLyrics();
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
        _lyricsTimer?.Dispose();
        _lyricsTimer = null;
    }
}
