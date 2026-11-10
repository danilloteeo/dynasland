using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;
using Dynasland.Modules.Timer;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

internal sealed class FakeFocusStore : IFocusSessionStore
{
    public List<FocusSession> Sessions { get; } = [];

    public Task AddAsync(FocusSession session, CancellationToken cancellationToken = default)
    {
        lock (Sessions)
        {
            Sessions.Add(session);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FocusSession>> ListSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        lock (Sessions)
        {
            return Task.FromResult<IReadOnlyList<FocusSession>>([.. Sessions.Where(s => s.StartedAt >= since)]);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Sessions.Clear();
        return Task.CompletedTask;
    }
}

public class PomodoroTests : IDisposable
{
    private static readonly PomodoroConfig Short = new(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), 2, true);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
    private readonly ActivityRecorder _recorder = new();

    public void Dispose() => _recorder.Dispose();

    [Fact]
    public void Cycles_focus_short_break_and_long_break()
    {
        var engine = new PomodoroEngine(_time);
        engine.Start(Short);

        Assert.Equal(PomodoroPhase.Focus, engine.Phase);
        Assert.Equal(1, engine.CycleNumber);

        _time.Advance(TimeSpan.FromMinutes(25));
        Assert.True(engine.IsPhaseFinished);
        var first = engine.CompletePhase();
        Assert.True(first.Completed);
        Assert.Equal(TimeSpan.FromMinutes(25), first.Actual);
        Assert.Equal(PomodoroPhase.ShortBreak, engine.Phase);
        Assert.Equal(1, engine.CycleNumber);

        _time.Advance(TimeSpan.FromMinutes(5));
        engine.CompletePhase();
        Assert.Equal(PomodoroPhase.Focus, engine.Phase);
        Assert.Equal(2, engine.CycleNumber);

        _time.Advance(TimeSpan.FromMinutes(25));
        engine.CompletePhase();
        Assert.Equal(PomodoroPhase.LongBreak, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(15), engine.Remaining);
        Assert.Equal(2, engine.CompletedFocusCount);
    }

    [Fact]
    public void Remaining_uses_real_time_so_it_survives_sleep_and_pauses()
    {
        var engine = new PomodoroEngine(_time);
        engine.Start(Short);

        _time.Advance(TimeSpan.FromMinutes(10));
        engine.Pause();
        _time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(TimeSpan.FromMinutes(15), engine.Remaining);

        engine.Resume();
        _time.Advance(TimeSpan.FromMinutes(40));
        Assert.True(engine.IsPhaseFinished);
    }

    [Fact]
    public void Skipped_focus_does_not_count_and_without_autostart_next_phase_waits()
    {
        var engine = new PomodoroEngine(_time);
        engine.Start(Short with { AutoStartNext = false });

        _time.Advance(TimeSpan.FromMinutes(3));
        var skipped = engine.Skip();

        Assert.False(skipped.Completed);
        Assert.Equal(TimeSpan.FromMinutes(3), skipped.Actual);
        Assert.Equal(0, engine.CompletedFocusCount);
        Assert.Equal(PomodoroPhase.ShortBreak, engine.Phase);
        Assert.False(engine.IsRunning);
        _time.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(TimeSpan.FromMinutes(5), engine.Remaining);
    }

    [Fact]
    public async Task Module_runs_pomodoro_announces_phase_end_and_records_history()
    {
        var history = new FakeFocusStore();
        var options = TestOptions.Create();
        var module = new TimerModule(_recorder.Bus, new InlineDispatcher(), _time, new ShortcutRegistry(), new SilentSound(), options, history, new GlobalShortcutRegistry(new InMemorySettingsStore()));
        await module.StartAsync(CancellationToken.None);

        module.Pomodoro!.StartOrToggle();
        Assert.Contains(_recorder.Posted, a => a.Id == PomodoroController.ActivityId && a.Kind == ActivityKind.Live);

        _time.Advance(TimeSpan.FromMinutes(25));
        await WaitUntil(() => history.Sessions.Count == 1);

        Assert.Contains(_recorder.Posted, a => a.Id == PomodoroController.PhaseEndedActivityId);
        Assert.Equal(FocusSessionKind.Focus, history.Sessions[0].Kind);
        Assert.True(history.Sessions[0].Completed);

        module.Pomodoro.Stop();
        Assert.Contains(PomodoroController.ActivityId, _recorder.Removed);
        await module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Stopwatch_laps_record_split_times()
    {
        var stopwatch = new StopwatchEngine(_time);
        stopwatch.Start();

        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(30), stopwatch.Lap());
        _time.Advance(TimeSpan.FromSeconds(45));
        Assert.Equal(TimeSpan.FromSeconds(45), stopwatch.Lap());

        Assert.Equal([TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(75)], stopwatch.Laps);
        stopwatch.Reset();
        Assert.Empty(stopwatch.Laps);
        Assert.Null(stopwatch.Lap());
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}

internal sealed class SilentSound : IAlertSound
{
    public void Play()
    {
    }
}
