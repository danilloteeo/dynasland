using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Timer;

public sealed partial class StopwatchContent(Action pauseResume, Action stop) : ObservableObject, IActivityContent
{
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
        IslandState.Interactive => new IslandDimensions(360, 120, 32),
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
