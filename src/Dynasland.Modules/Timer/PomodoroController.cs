using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;

namespace Dynasland.Modules.Timer;

/// <summary>
/// Pomodoro do Focus Center: atividade contínua enquanto ativo, aviso ao trocar de fase, alarme e
/// registro de cada fase no histórico de foco. Parte do <see cref="TimerModule"/>.
/// </summary>
public sealed class PomodoroController(
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IAlertSound sound,
    IModuleOptions options,
    IFocusSessionStore history) : IDisposable
{
    public const string ActivityId = "timer.pomodoro";
    public const string PhaseEndedActivityId = "timer.pomodoro.phase";

    public static readonly ModuleOption FocusMinutes = ModuleOption.Slider(
        "pomodoroFocus", "Pomodoro: foco", "Duração de cada período de foco.", 25, 5, 90, 5, "min");

    public static readonly ModuleOption ShortBreakMinutes = ModuleOption.Slider(
        "pomodoroShortBreak", "Pomodoro: pausa curta", "Pausa entre os focos.", 5, 1, 30, 1, "min");

    public static readonly ModuleOption LongBreakMinutes = ModuleOption.Slider(
        "pomodoroLongBreak", "Pomodoro: pausa longa", "Pausa depois de completar o ciclo.", 15, 5, 60, 5, "min");

    public static readonly ModuleOption CyclesBeforeLongBreak = ModuleOption.Slider(
        "pomodoroCycles", "Pomodoro: focos por ciclo", "Quantos focos até a pausa longa.", 4, 2, 8, 1, "");

    public static readonly ModuleOption AutoStartNext = ModuleOption.Toggle(
        "pomodoroAutoStart", "Pomodoro: seguir automaticamente", "Ao terminar uma fase, a próxima já começa (desligado, espera você continuar).");

    public static IReadOnlyList<ModuleOption> Options => [FocusMinutes, ShortBreakMinutes, LongBreakMinutes, CyclesBeforeLongBreak, AutoStartNext];

    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(500);

    private readonly Lock _lock = new();
    private readonly PomodoroEngine _engine = new(time);
    private PomodoroContent? _content;
    private StatusContent? _phaseEnded;
    private ITimer? _ticker;
    private ITimer? _phaseTimer;
    private Func<string, bool>? _alarmEnabled;

    public bool IsActive
    {
        get
        {
            lock (_lock)
            {
                return _engine.IsActive;
            }
        }
    }

    /// <param name="alarmEnabled">Consulta a opção de alarme do módulo.</param>
    public void Initialize(Func<string, bool> alarmEnabled)
    {
        _alarmEnabled = alarmEnabled;
        _content = new PomodoroContent(PauseResume, Skip, Stop);
        _phaseEnded = new StatusContent(StatusKind.Info, string.Empty);
    }

    public PomodoroConfig ReadConfig() => new(
        TimeSpan.FromMinutes(options.Get(TimerModule.ModuleId, FocusMinutes)),
        TimeSpan.FromMinutes(options.Get(TimerModule.ModuleId, ShortBreakMinutes)),
        TimeSpan.FromMinutes(options.Get(TimerModule.ModuleId, LongBreakMinutes)),
        (int)options.Get(TimerModule.ModuleId, CyclesBeforeLongBreak),
        options.IsOn(TimerModule.ModuleId, AutoStartNext));

    /// <summary>Atalho global e da tela inicial: inicia, ou pausa/continua se já estiver ativo.</summary>
    public void StartOrToggle()
    {
        bool started;
        lock (_lock)
        {
            if (_content is null)
            {
                return;
            }

            started = !_engine.IsActive;
            if (started)
            {
                _engine.Start(ReadConfig());
            }
            else if (_engine.IsRunning)
            {
                _engine.Pause();
            }
            else
            {
                _engine.Resume();
            }

            Reschedule();
        }

        Refresh();
        if (started)
        {
            _ = RefreshTodayAsync();
            bus.PostActivity(new IslandActivity(ActivityId, TimerModule.ModuleId, ActivityKind.Live, _content) { ExpandOnStart = true });
        }
    }

    public void PauseResume()
    {
        lock (_lock)
        {
            if (!_engine.IsActive)
            {
                return;
            }

            if (_engine.IsRunning)
            {
                _engine.Pause();
            }
            else
            {
                _engine.Resume();
            }

            Reschedule();
        }

        Refresh();
    }

    public void Skip()
    {
        PomodoroPhaseResult result;
        lock (_lock)
        {
            if (!_engine.IsActive)
            {
                return;
            }

            result = _engine.Skip();
            Reschedule();
        }

        _ = SaveAsync(result);
        Refresh();
    }

    public void Stop()
    {
        PomodoroPhaseResult? result;
        lock (_lock)
        {
            result = _engine.Stop();
            Reschedule();
        }

        if (result is not null)
        {
            _ = SaveAsync(result);
        }

        bus.RemoveActivity(ActivityId);
    }

    /// <summary>Após suspender o computador os timers atrasam; o fim da fase é por instante, então reagenda.</summary>
    public void OnResumed()
    {
        lock (_lock)
        {
            Reschedule();
        }

        Refresh();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _ticker?.Dispose();
            _phaseTimer?.Dispose();
            _ticker = null;
            _phaseTimer = null;
            _engine.Stop();
        }
    }

    private void OnPhaseTimer()
    {
        PomodoroPhaseResult result;
        PomodoroPhase next;
        bool running;
        lock (_lock)
        {
            if (!_engine.IsPhaseFinished)
            {
                Reschedule();
                return;
            }

            result = _engine.CompletePhase();
            next = _engine.Phase;
            running = _engine.IsRunning;
            Reschedule();
        }

        _ = SaveAsync(result);
        Refresh();
        AnnouncePhaseEnd(result.Phase, next, running);
        if (_alarmEnabled?.Invoke(TimerModule.ModuleId) == true)
        {
            sound.Play();
        }
    }

    private void AnnouncePhaseEnd(PomodoroPhase ended, PomodoroPhase next, bool running)
    {
        if (_phaseEnded is not { } status)
        {
            return;
        }

        var title = ended == PomodoroPhase.Focus ? "Foco concluído" : "Pausa terminada";
        var message = running
            ? $"{PomodoroContent.PhaseName(next)} começou."
            : $"Toque na ilha para começar: {PomodoroContent.PhaseName(next).ToLowerInvariant()}.";
        ui.Post(() =>
        {
            status.Title = title;
            status.Message = message;
        });
        bus.PostActivity(new IslandActivity(PhaseEndedActivityId, TimerModule.ModuleId, ActivityKind.Transient, status)
        {
            Priority = ActivityPriority.High,
            Duration = TimeSpan.FromSeconds(8),
        });
    }

    /// <summary>Timers: um para o fim exato da fase e um de atualização só enquanto conta. Chamar com o lock.</summary>
    private void Reschedule()
    {
        _phaseTimer?.Dispose();
        _phaseTimer = _engine is { IsActive: true, IsRunning: true }
            ? time.CreateTimer(_ => OnPhaseTimer(), null, _engine.Remaining, Timeout.InfiniteTimeSpan)
            : null;

        var needsTicker = _engine is { IsActive: true, IsRunning: true };
        if (needsTicker && _ticker is null)
        {
            _ticker = time.CreateTimer(_ => Refresh(), null, TickInterval, TickInterval);
        }
        else if (!needsTicker && _ticker is not null)
        {
            _ticker.Dispose();
            _ticker = null;
        }
    }

    private void Refresh()
    {
        PomodoroPhase phase;
        int cycle, cycles;
        TimeSpan remaining;
        double progress;
        bool running;
        lock (_lock)
        {
            if (!_engine.IsActive || _content is null)
            {
                return;
            }

            phase = _engine.Phase;
            cycle = _engine.CycleNumber;
            cycles = _engine.Config.CyclesBeforeLongBreak;
            remaining = _engine.Remaining;
            progress = _engine.Progress;
            running = _engine.IsRunning;
        }

        var content = _content;
        ui.Post(() => content.Update(phase, cycle, cycles, remaining, progress, running));
    }

    private async Task SaveAsync(PomodoroPhaseResult result)
    {
        // Fases interrompidas com menos de um minuto não interessam ao histórico.
        if (!result.Completed && result.Actual < TimeSpan.FromMinutes(1))
        {
            return;
        }

        var kind = result.Phase switch
        {
            PomodoroPhase.ShortBreak => FocusSessionKind.ShortBreak,
            PomodoroPhase.LongBreak => FocusSessionKind.LongBreak,
            _ => FocusSessionKind.Focus,
        };

        try
        {
            await history.AddAsync(new FocusSession(0, kind, result.StartedAt, result.EndedAt, result.Planned, result.Actual, result.Completed));
            await RefreshTodayAsync();
        }
        catch (Exception)
        {
            // Sem histórico (banco indisponível) o Pomodoro continua funcionando.
        }
    }

    private async Task RefreshTodayAsync()
    {
        if (_content is not { } content)
        {
            return;
        }

        var startOfDay = new DateTimeOffset(time.GetLocalNow().Date, time.GetLocalNow().Offset);
        try
        {
            var sessions = await history.ListSinceAsync(startOfDay);
            var focus = sessions.Where(s => s.Kind == FocusSessionKind.Focus && s.Completed).ToList();
            var total = TimeSpan.FromTicks(focus.Sum(s => s.Actual.Ticks));
            ui.Post(() => content.SetToday(focus.Count, total));
        }
        catch (Exception)
        {
            ui.Post(() => content.TodayText = string.Empty);
        }
    }
}
