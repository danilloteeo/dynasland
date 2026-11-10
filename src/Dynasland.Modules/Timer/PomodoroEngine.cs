namespace Dynasland.Modules.Timer;

public enum PomodoroPhase
{
    Focus,
    ShortBreak,
    LongBreak,
}

/// <param name="CyclesBeforeLongBreak">Focos antes da pausa longa (a pausa longa vem depois do N-ésimo foco).</param>
/// <param name="AutoStartNext">Ao terminar uma fase, a seguinte já começa contando; senão espera o usuário.</param>
public sealed record PomodoroConfig(TimeSpan Focus, TimeSpan ShortBreak, TimeSpan LongBreak, int CyclesBeforeLongBreak, bool AutoStartNext)
{
    public static PomodoroConfig Default { get; } = new(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), 4, true);

    public TimeSpan DurationOf(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.ShortBreak => ShortBreak,
        PomodoroPhase.LongBreak => LongBreak,
        _ => Focus,
    };
}

/// <summary>Fase encerrada (para o histórico e o aviso na ilha).</summary>
public sealed record PomodoroPhaseResult(PomodoroPhase Phase, DateTimeOffset StartedAt, DateTimeOffset EndedAt, TimeSpan Planned, TimeSpan Actual, bool Completed);

/// <summary>
/// Pomodoro baseado em instantes reais (como o temporizador): o tempo restante é calculado a partir do
/// relógio, então continua certo depois de suspender o computador. Não é thread-safe.
/// </summary>
public sealed class PomodoroEngine(TimeProvider time)
{
    private readonly CountdownEngine _countdown = new(time);
    private DateTimeOffset _phaseStartedAt;

    public PomodoroConfig Config { get; private set; } = PomodoroConfig.Default;

    public bool IsActive { get; private set; }

    public bool IsRunning => _countdown.IsRunning;

    public PomodoroPhase Phase { get; private set; }

    /// <summary>Focos concluídos desde o início da série.</summary>
    public int CompletedFocusCount { get; private set; }

    /// <summary>Posição do foco atual (ou do último, numa pausa) dentro do ciclo, de 1 a N.</summary>
    public int CycleNumber => Phase == PomodoroPhase.Focus
        ? CompletedFocusCount % Config.CyclesBeforeLongBreak + 1
        : (CompletedFocusCount - 1) % Config.CyclesBeforeLongBreak + 1;

    public TimeSpan Remaining => _countdown.Remaining;

    public TimeSpan PhaseDuration => _countdown.Duration;

    public double Progress => _countdown.Progress;

    public bool IsPhaseFinished => IsActive && _countdown.IsFinished;

    public void Start(PomodoroConfig config)
    {
        Config = config with { CyclesBeforeLongBreak = Math.Max(1, config.CyclesBeforeLongBreak) };
        CompletedFocusCount = 0;
        IsActive = true;
        BeginPhase(PomodoroPhase.Focus, running: true);
    }

    public void Pause() => _countdown.Pause();

    public void Resume()
    {
        if (IsActive)
        {
            _countdown.Resume();
        }
    }

    /// <summary>A fase chegou ao fim: registra e passa para a próxima.</summary>
    public PomodoroPhaseResult CompletePhase() => Advance(completed: true);

    /// <summary>Pula a fase atual (não conta como concluída) e passa para a próxima.</summary>
    public PomodoroPhaseResult Skip() => Advance(completed: false);

    /// <returns>A fase interrompida, se havia uma em andamento.</returns>
    public PomodoroPhaseResult? Stop()
    {
        if (!IsActive)
        {
            return null;
        }

        var result = Result(completed: false);
        _countdown.Cancel();
        IsActive = false;
        return result;
    }

    private PomodoroPhaseResult Advance(bool completed)
    {
        var result = Result(completed);
        if (Phase == PomodoroPhase.Focus && completed)
        {
            CompletedFocusCount++;
        }

        var next = Phase != PomodoroPhase.Focus
            ? PomodoroPhase.Focus
            : completed && CompletedFocusCount % Config.CyclesBeforeLongBreak == 0
                ? PomodoroPhase.LongBreak
                : PomodoroPhase.ShortBreak;

        BeginPhase(next, running: Config.AutoStartNext);
        return result;
    }

    private void BeginPhase(PomodoroPhase phase, bool running)
    {
        Phase = phase;
        _phaseStartedAt = time.GetUtcNow();
        _countdown.Start(Config.DurationOf(phase));
        if (!running)
        {
            _countdown.Pause();
        }
    }

    private PomodoroPhaseResult Result(bool completed)
    {
        var now = time.GetUtcNow();
        var planned = _countdown.Duration;
        var actual = completed ? planned : planned - _countdown.Remaining;
        return new PomodoroPhaseResult(Phase, _phaseStartedAt, now, planned, actual < TimeSpan.Zero ? TimeSpan.Zero : actual, completed);
    }
}
