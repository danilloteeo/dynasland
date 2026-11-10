using Dynasland.Core.Activities;
using Dynasland.Core.Shortcuts;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;
using Dynasland.Modules.Timer;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class TimerTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly ActivityRecorder _recorder = new();
    private readonly ShortcutRegistry _shortcuts = new();
    private readonly FakeSound _sound = new();

    public void Dispose() => _recorder.Dispose();

    private readonly FakeFocusStore _history = new();

    private TimerModule CreateModule() => new(_recorder.Bus, new InlineDispatcher(), _time, _shortcuts, _sound, TestOptions.Create(), _history, new GlobalShortcutRegistry(new InMemorySettingsStore()));

    [Fact]
    public void Stopwatch_counts_pauses_and_resets()
    {
        var stopwatch = new StopwatchEngine(_time);

        stopwatch.Start();
        _time.Advance(TimeSpan.FromSeconds(10));
        stopwatch.Pause();
        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(10), stopwatch.Elapsed);

        stopwatch.Resume();
        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(15), stopwatch.Elapsed);

        stopwatch.Reset();
        Assert.False(stopwatch.IsActive);
        Assert.Equal(TimeSpan.Zero, stopwatch.Elapsed);
    }

    [Fact]
    public void Countdown_pauses_adds_time_and_finishes()
    {
        var countdown = new CountdownEngine(_time);

        countdown.Start(TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromSeconds(20));
        countdown.Pause();
        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromSeconds(40), countdown.Remaining);

        countdown.Add(TimeSpan.FromMinutes(1));
        countdown.Resume();
        Assert.Equal(TimeSpan.FromSeconds(100), countdown.Remaining);
        Assert.Equal(TimeSpan.FromMinutes(2), countdown.Duration);

        _time.Advance(TimeSpan.FromSeconds(100));
        Assert.True(countdown.IsFinished);
        Assert.Equal(1, countdown.Progress);
    }

    [Fact]
    public async Task Module_registers_home_shortcuts_only_while_running()
    {
        var module = CreateModule();

        await module.StartAsync(CancellationToken.None);
        Assert.Equal(["Cronômetro", "Temporizador", "Pomodoro"], _shortcuts.Shortcuts.Select(s => s.Label));

        await module.StopAsync(CancellationToken.None);
        Assert.Empty(_shortcuts.Shortcuts);
    }

    [Fact]
    public async Task Stopwatch_shortcut_starts_live_activity_that_expands()
    {
        var module = CreateModule();
        await module.StartAsync(CancellationToken.None);

        _shortcuts.Shortcuts.Single(s => s.Label == "Cronômetro").Invoke();
        _time.Advance(TimeSpan.FromSeconds(65.2));

        var activity = Assert.Single(_recorder.Posted);
        Assert.Equal(TimerModule.StopwatchActivityId, activity.Id);
        Assert.Equal(ActivityKind.Live, activity.Kind);
        Assert.True(activity.ExpandOnStart);
        var content = Assert.IsType<StopwatchContent>(activity.Content);
        Assert.Equal("01:05", content.ElapsedText);

        content.StopCommand.Execute(null);
        Assert.Contains(TimerModule.StopwatchActivityId, _recorder.Removed);
    }

    [Fact]
    public async Task Countdown_from_setup_finishes_with_alert_and_sound()
    {
        var module = CreateModule();
        await module.StartAsync(CancellationToken.None);

        _shortcuts.Shortcuts.Single(s => s.Label == "Temporizador").Invoke();
        var setup = Assert.Single(_recorder.Posted);
        Assert.True(setup.OpenInteractive);
        var presets = Assert.IsType<TimerSetupContent>(setup.Content).Presets;
        Assert.Equal("5 min", presets[2].Label);

        presets[0].Command.Execute(null); // 1 min
        Assert.Contains(TimerModule.SetupActivityId, _recorder.Removed);
        var countdown = Assert.IsType<CountdownContent>(_recorder.PostedWithId(TimerModule.CountdownActivityId).Single().Content);

        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal("00:30", countdown.RemainingText);
        Assert.Equal(0, _sound.Plays);

        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(1, _sound.Plays);
        Assert.Contains(TimerModule.CountdownActivityId, _recorder.Removed);
        var finished = _recorder.PostedWithId(TimerModule.FinishedActivityId).Single();
        Assert.Equal(ActivityPriority.High, finished.Priority);
        Assert.Equal("Temporizador de 1 min", Assert.IsType<TimerFinishedContent>(finished.Content).DurationText);
    }

    [Fact]
    public async Task Paused_countdown_does_not_finish()
    {
        var module = CreateModule();
        await module.StartAsync(CancellationToken.None);
        module.StartCountdown(TimeSpan.FromSeconds(10));
        var countdown = (CountdownContent)_recorder.Posted.Single().Content;

        countdown.PauseResumeCommand.Execute(null);
        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(0, _sound.Plays);

        countdown.PauseResumeCommand.Execute(null);
        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, _sound.Plays);
    }

    [Theory]
    [InlineData(65, "01:05")]
    [InlineData(3725, "1:02:05")]
    public void Timer_text_is_formatted(int seconds, string expected)
    {
        Assert.Equal(expected, TextFormat.Timer(TimeSpan.FromSeconds(seconds)));
    }

    private sealed class FakeSound : IAlertSound
    {
        public int Plays { get; private set; }

        public void Play() => Plays++;
    }
}
