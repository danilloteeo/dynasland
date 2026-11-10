using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Core.Models;
using Dynasland.Core.Session;
using Dynasland.Core.Shortcuts;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Timer;

/// <summary>
/// Cronômetro e temporizador regressivo. Iniciados pelos atalhos da tela inicial; enquanto rodam
/// aparecem como atividade contínua (indicador no compacto) e o fim do temporizador toca um alerta.
/// </summary>
public sealed class TimerModule(
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IShortcutRegistry shortcuts,
    IAlertSound sound,
    IModuleOptions options,
    IFocusSessionStore history,
    IGlobalShortcutRegistry globalShortcuts) : IIslandModule
{
    public const string ModuleId = "timer";
    public const string StopwatchActivityId = "timer.stopwatch";
    public const string CountdownActivityId = "timer.countdown";
    public const string SetupActivityId = "timer.setup";
    public const string FinishedActivityId = "timer.finished";

    public static readonly int[] PresetMinutes = [1, 3, 5, 10, 15, 25, 30, 60];

    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan FinishedDisplayDuration = TimeSpan.FromSeconds(15);

    private readonly Lock _lock = new();
    private readonly List<IDisposable> _registrations = [];
    private StopwatchEngine? _stopwatch;
    private CountdownEngine? _countdown;
    private StopwatchContent? _stopwatchContent;
    private CountdownContent? _countdownContent;
    private TimerSetupContent? _setupContent;
    private TimerFinishedContent? _finishedContent;
    private ITimer? _ticker;
    private ITimer? _finishTimer;
    private TimeSpan _lastDuration;
    private DateTimeOffset _stopwatchStartedAt;
    private DateTimeOffset _countdownStartedAt;
    private PomodoroController? _pomodoro;

    public string Id => ModuleId;

    public string DisplayName => "Focus Center";

    public string Description => "Cronômetro com voltas, temporizador e Pomodoro direto na ilha, com alerta e histórico de foco.";

    public static readonly ModuleOption AlarmSound = ModuleOption.Toggle(
        "alarmSound", "Tocar alarme", "Toca o som de alarme do Windows quando o temporizador termina.");

    public IReadOnlyList<ModuleOption> Options => [AlarmSound, .. PomodoroController.Options];

    public PomodoroController? Pomodoro => _pomodoro;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _stopwatch = new StopwatchEngine(time);
            _countdown = new CountdownEngine(time);
            _stopwatchContent = new StopwatchContent(ToggleStopwatch, StopStopwatch, MarkLap);
            _countdownContent = new CountdownContent(ToggleCountdown, AddMinute, CancelCountdown);
            _setupContent = new TimerSetupContent(PresetMinutes, minutes => StartCountdown(TimeSpan.FromMinutes(minutes)));
            _finishedContent = new TimerFinishedContent(
                () => bus.RemoveActivity(FinishedActivityId),
                () => StartCountdown(_lastDuration));
        }

        _registrations.Add(shortcuts.Register(new IslandShortcut("timer.stopwatch", ModuleId, Glyphs.Stopwatch, "Cronômetro", StartStopwatch)));
        _registrations.Add(shortcuts.Register(new IslandShortcut("timer.countdown", ModuleId, Glyphs.Timer, "Temporizador", OpenSetup)));
        _registrations.Add(bus.Subscribe<SessionChangedEvent>(OnSessionChanged));

        _pomodoro = new PomodoroController(bus, ui, time, sound, options, history);
        _pomodoro.Initialize(_ => options.IsOn(ModuleId, AlarmSound));
        _registrations.Add(shortcuts.Register(new IslandShortcut("timer.pomodoro", ModuleId, Glyphs.Flag, "Pomodoro", _pomodoro.StartOrToggle)));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut(
            "timer.pomodoro", "Pomodoro: iniciar/pausar", "Focus Center",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'P'), _pomodoro.StartOrToggle)));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut(
            "timer.stopwatch", "Cronômetro: iniciar/pausar", "Focus Center", null, StartOrToggleStopwatch)));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
        _pomodoro?.Dispose();
        _pomodoro = null;
        lock (_lock)
        {
            _ticker?.Dispose();
            _finishTimer?.Dispose();
            _ticker = null;
            _finishTimer = null;
            _stopwatch?.Reset();
            _countdown?.Cancel();
        }

        return Task.CompletedTask;
    }

    public void StartStopwatch()
    {
        lock (_lock)
        {
            if (_stopwatch is null)
            {
                return;
            }

            _stopwatch.Start();
            _stopwatchStartedAt = time.GetUtcNow();
            UpdateTicker();
        }

        Refresh();
        bus.PostActivity(new IslandActivity(StopwatchActivityId, ModuleId, ActivityKind.Live, _stopwatchContent!) { ExpandOnStart = true });
    }

    public void OpenSetup()
    {
        if (_setupContent is { } setup)
        {
            bus.PostActivity(new IslandActivity(SetupActivityId, ModuleId, ActivityKind.Transient, setup) { OpenInteractive = true });
        }
    }

    public void StartCountdown(TimeSpan duration)
    {
        lock (_lock)
        {
            if (_countdown is null)
            {
                return;
            }

            _lastDuration = duration;
            _countdown.Start(duration);
            _countdownStartedAt = time.GetUtcNow();
            ScheduleFinish();
            UpdateTicker();
        }

        bus.RemoveActivity(SetupActivityId);
        bus.RemoveActivity(FinishedActivityId);
        Refresh();
        bus.PostActivity(new IslandActivity(CountdownActivityId, ModuleId, ActivityKind.Live, _countdownContent!) { ExpandOnStart = true });
    }

    private void ToggleStopwatch()
    {
        lock (_lock)
        {
            if (_stopwatch is null)
            {
                return;
            }

            if (_stopwatch.IsRunning)
            {
                _stopwatch.Pause();
            }
            else
            {
                _stopwatch.Resume();
            }

            UpdateTicker();
        }

        Refresh();
    }

    /// <summary>Atalho global: inicia o cronômetro, ou pausa/continua se já estiver ativo.</summary>
    private void StartOrToggleStopwatch()
    {
        bool active;
        lock (_lock)
        {
            active = _stopwatch?.IsActive == true;
        }

        if (active)
        {
            ToggleStopwatch();
        }
        else
        {
            StartStopwatch();
        }
    }

    private void MarkLap()
    {
        lock (_lock)
        {
            _stopwatch?.Lap();
        }

        Refresh();
    }

    private void StopStopwatch()
    {
        TimeSpan elapsed;
        lock (_lock)
        {
            elapsed = _stopwatch?.Elapsed ?? TimeSpan.Zero;
            _stopwatch?.Reset();
            UpdateTicker();
        }

        if (elapsed >= TimeSpan.FromMinutes(1))
        {
            Save(FocusSessionKind.Stopwatch, _stopwatchStartedAt, elapsed, elapsed, completed: true);
        }

        bus.RemoveActivity(StopwatchActivityId);
    }

    private void ToggleCountdown()
    {
        lock (_lock)
        {
            if (_countdown is null)
            {
                return;
            }

            if (_countdown.IsRunning)
            {
                _countdown.Pause();
            }
            else
            {
                _countdown.Resume();
            }

            ScheduleFinish();
            UpdateTicker();
        }

        Refresh();
    }

    private void AddMinute()
    {
        lock (_lock)
        {
            _countdown?.Add(TimeSpan.FromMinutes(1));
            ScheduleFinish();
        }

        Refresh();
    }

    private void CancelCountdown()
    {
        lock (_lock)
        {
            if (_countdown is { IsActive: true } running && running.Duration - running.Remaining >= TimeSpan.FromMinutes(1))
            {
                Save(FocusSessionKind.Timer, _countdownStartedAt, running.Duration, running.Duration - running.Remaining, completed: false);
            }

            _countdown?.Cancel();
            ScheduleFinish();
            UpdateTicker();
        }

        bus.RemoveActivity(CountdownActivityId);
    }

    private void Finish()
    {
        lock (_lock)
        {
            if (_countdown is not { IsFinished: true })
            {
                return;
            }

            Save(FocusSessionKind.Timer, _countdownStartedAt, _countdown.Duration, _countdown.Duration, completed: true);
            _countdown.Cancel();
            ScheduleFinish();
            UpdateTicker();
        }

        bus.RemoveActivity(CountdownActivityId);
        if (_finishedContent is { } finished)
        {
            var text = $"Temporizador de {TextFormat.Duration(_lastDuration)}";
            ui.Post(() => finished.DurationText = text);
            bus.PostActivity(new IslandActivity(FinishedActivityId, ModuleId, ActivityKind.Transient, finished)
            {
                Priority = ActivityPriority.High,
                Duration = FinishedDisplayDuration,
            });
        }

        if (options.IsOn(ModuleId, AlarmSound))
        {
            sound.Play();
        }
    }

    /// <summary>
    /// Os timers do .NET não contam o tempo em suspensão; o término é por instante, então ao retomar
    /// reagenda: se o fim passou durante a suspensão, o alerta dispara agora.
    /// </summary>
    private void OnSessionChanged(SessionChangedEvent e)
    {
        if (e.Change != SessionChange.Resumed)
        {
            return;
        }

        lock (_lock)
        {
            ScheduleFinish();
        }

        _pomodoro?.OnResumed();
        Refresh();
    }

    private void Save(FocusSessionKind kind, DateTimeOffset startedAt, TimeSpan planned, TimeSpan actual, bool completed)
    {
        var session = new FocusSession(0, kind, startedAt, time.GetUtcNow(), planned, actual, completed);
        _ = Task.Run(async () =>
        {
            try
            {
                await history.AddAsync(session);
            }
            catch (Exception)
            {
                // Histórico é complementar: falha no banco não afeta o cronômetro.
            }
        });
    }

    /// <summary>Agenda o fim exato do temporizador. Chamar com <see cref="_lock"/> adquirido.</summary>
    private void ScheduleFinish()
    {
        _finishTimer?.Dispose();
        _finishTimer = _countdown is { IsRunning: true } countdown
            ? time.CreateTimer(_ => Finish(), null, countdown.Remaining, Timeout.InfiniteTimeSpan)
            : null;
    }

    /// <summary>O relógio de atualização só existe enquanto algo está contando. Chamar com <see cref="_lock"/>.</summary>
    private void UpdateTicker()
    {
        var needed = _stopwatch?.IsRunning == true || _countdown?.IsRunning == true;
        if (needed && _ticker is null)
        {
            _ticker = time.CreateTimer(_ => Refresh(), null, TickInterval, TickInterval);
        }
        else if (!needed && _ticker is not null)
        {
            _ticker.Dispose();
            _ticker = null;
        }
    }

    private void Refresh()
    {
        TimeSpan elapsed, remaining, duration;
        bool stopwatchRunning, countdownRunning;
        double progress;
        TimeSpan[] laps;
        lock (_lock)
        {
            if (_stopwatch is null || _countdown is null)
            {
                return;
            }

            elapsed = _stopwatch.Elapsed;
            laps = [.. _stopwatch.Laps];
            stopwatchRunning = _stopwatch.IsRunning;
            remaining = _countdown.Remaining;
            duration = _countdown.Duration;
            progress = _countdown.Progress;
            countdownRunning = _countdown.IsRunning;
        }

        var stopwatchContent = _stopwatchContent;
        var countdownContent = _countdownContent;
        ui.Post(() =>
        {
            stopwatchContent?.Update(elapsed, stopwatchRunning);
            stopwatchContent?.SetLaps(laps);
            countdownContent?.Update(remaining, duration, progress, countdownRunning);
        });
    }
}
