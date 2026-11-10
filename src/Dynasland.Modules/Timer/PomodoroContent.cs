using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Timer;

public sealed partial class PomodoroContent(Action pauseResume, Action skip, Action stop) : ObservableObject, IActivityContent
{
    [ObservableProperty]
    private string _remainingText = "25:00";

    [ObservableProperty]
    private string _phaseText = "Foco";

    [ObservableProperty]
    private string _cycleText = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isBreak;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseResumeGlyph))]
    private bool _isRunning;

    [ObservableProperty]
    private string _todayText = string.Empty;

    public string Glyph => Glyphs.Flag;

    public string PauseResumeGlyph => IsRunning ? Glyphs.Pause : Glyphs.Play;

    public string SkipGlyph => Glyphs.Skip;

    public string StopGlyph => Glyphs.Stop;

    public static string PhaseName(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.ShortBreak => "Pausa curta",
        PomodoroPhase.LongBreak => "Pausa longa",
        _ => "Foco",
    };

    public void Update(PomodoroPhase phase, int cycle, int cycles, TimeSpan remaining, double progress, bool running)
    {
        PhaseText = running || remaining < TimeSpan.FromSeconds(1) ? PhaseName(phase) : $"{PhaseName(phase)} (pausado)";
        CycleText = $"Ciclo {cycle} de {cycles}";
        RemainingText = TextFormat.Timer(TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds)));
        Progress = progress;
        IsBreak = phase != PomodoroPhase.Focus;
        IsRunning = running;
    }

    public void SetToday(int focusCount, TimeSpan focusTime) =>
        TodayText = focusCount == 0
            ? "Nenhum foco concluído hoje"
            : $"Hoje: {focusCount} {(focusCount == 1 ? "foco" : "focos")} · {TextFormat.Duration(focusTime)}";

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Compact => new IslandDimensions(240, 34, 17),
        IslandState.Expanded => new IslandDimensions(320, 64, 30),
        IslandState.Interactive => new IslandDimensions(390, 150, 34),
        _ => null,
    };

    [RelayCommand]
    private void PauseResume() => pauseResume();

    [RelayCommand]
    private void Skip() => skip();

    [RelayCommand]
    private void Stop() => stop();
}
