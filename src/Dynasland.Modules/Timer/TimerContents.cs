using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Timer;

/// <summary>Volta do cronômetro: número, duração da volta e tempo total.</summary>
public sealed record LapItem(int Number, string LapText, string TotalText);

public sealed partial class StopwatchContent(Action pauseResume, Action stop, Action lap) : ObservableObject, IActivityContent
{
    /// <summary>Últimas voltas, a mais recente primeiro.</summary>
    public System.Collections.ObjectModel.ObservableCollection<LapItem> Laps { get; } = [];

    public bool HasLaps => Laps.Count > 0;

    public string LapGlyph => Glyphs.Flag;

    public void SetLaps(IReadOnlyList<TimeSpan> totals)
    {
        if (totals.Count == (Laps.Count == 0 ? 0 : Laps[0].Number))
        {
            return;
        }

        Laps.Clear();
        for (var i = totals.Count - 1; i >= 0 && Laps.Count < 3; i--)
        {
            var previous = i > 0 ? totals[i - 1] : TimeSpan.Zero;
            Laps.Add(new LapItem(i + 1, TextFormat.Timer(totals[i] - previous), TextFormat.Timer(totals[i])));
        }

        OnPropertyChanged(nameof(HasLaps));
    }

    [RelayCommand]
    private void Lap() => lap();

    [ObservableProperty]
    private string _elapsedText = "00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseResumeGlyph))]
    private bool _isRunning;

    public string PauseResumeGlyph => IsRunning ? Glyphs.Pause : Glyphs.Play;

    public string Glyph => Glyphs.Stopwatch;

    public string StopGlyph => Glyphs.Stop;

    public void Update(TimeSpan elapsed, bool running)
    {
        ElapsedText = TextFormat.Timer(elapsed);
        IsRunning = running;
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Compact => new IslandDimensions(220, 34, 17),
        IslandState.Expanded => new IslandDimensions(300, 56, 28),
        IslandState.Interactive => new IslandDimensions(360, HasLaps ? 196 : 120, 32),
        _ => null,
    };

    [RelayCommand]
    private void PauseResume() => pauseResume();

    [RelayCommand]
    private void Stop() => stop();
}

public sealed partial class CountdownContent(Action pauseResume, Action addMinute, Action cancel) : ObservableObject, IActivityContent
{
    [ObservableProperty]
    private string _remainingText = "00:00";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseResumeGlyph))]
    private bool _isRunning;

    [ObservableProperty]
    private string _durationText = string.Empty;

    public string PauseResumeGlyph => IsRunning ? Glyphs.Pause : Glyphs.Play;

    public string Glyph => Glyphs.Timer;

    public string AddGlyph => Glyphs.Add;

    public string CancelGlyph => Glyphs.Cancel;

    public void Update(TimeSpan remaining, TimeSpan duration, double progress, bool running)
    {
        // Arredonda para cima: com 4,2 s restantes o temporizador mostra 00:05, como relógios de cozinha.
        RemainingText = TextFormat.Timer(TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds)));
        DurationText = $"Temporizador de {TextFormat.Duration(duration)}";
        Progress = progress;
        IsRunning = running;
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Compact => new IslandDimensions(220, 34, 17),
        IslandState.Expanded => new IslandDimensions(300, 56, 28),
        IslandState.Interactive => new IslandDimensions(380, 128, 32),
        _ => null,
    };

    [RelayCommand]
    private void PauseResume() => pauseResume();

    [RelayCommand]
    private void AddMinute() => addMinute();

    [RelayCommand]
    private void Cancel() => cancel();
}

public sealed record TimerPreset(string Label, IRelayCommand Command);

public sealed class TimerSetupContent : IActivityContent
{
    public TimerSetupContent(IEnumerable<int> presetMinutes, Action<int> start)
    {
        Presets = presetMinutes
            .Select(minutes => new TimerPreset(minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h", new RelayCommand(() => start(minutes))))
            .ToList();
    }

    public IReadOnlyList<TimerPreset> Presets { get; }

    public string Glyph => Glyphs.Timer;

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state == IslandState.Interactive ? new IslandDimensions(400, 132, 34) : null;
}

public sealed partial class TimerFinishedContent(Action dismiss, Action repeat) : ObservableObject, IActivityContent
{
    [ObservableProperty]
    private string _durationText = string.Empty;

    public string Glyph => Glyphs.Timer;

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(340, 64, 30),
        IslandState.Interactive => new IslandDimensions(360, 110, 32),
        _ => null,
    };

    [RelayCommand]
    private void Dismiss() => dismiss();

    [RelayCommand]
    private void Repeat() => repeat();
}
