using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Modules.Clock;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class ClockModuleTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 9, 14, 31, 50, TimeSpan.Zero));
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);
    private readonly InlineDispatcher _ui = new();
    private readonly ShortcutRegistry _shortcuts = new();

    public ClockModuleTests() => _time.SetLocalTimeZone(TimeZoneInfo.Utc);

    [Fact]
    public void Content_is_formatted_in_brazilian_portuguese()
    {
        var content = new ClockContent();

        content.Update(new DateTimeOffset(2026, 11, 9, 7, 5, 0, TimeSpan.Zero));

        Assert.Equal("07:05", content.TimeText);
        Assert.Equal("Segunda-feira, 9 de novembro", content.DateText);
    }

    [Fact]
    public async Task Start_publishes_low_priority_live_activity_with_current_time()
    {
        IslandActivity? posted = null;
        using var subscription = _bus.Subscribe<ActivityPostedEvent>(e => posted = e.Activity);
        using var module = new ClockModule(_bus, _ui, _time, _shortcuts, new GlanceRegistry());

        await module.StartAsync(CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(ClockModule.ActivityId, posted.Id);
        Assert.Equal(ActivityKind.Live, posted.Kind);
        Assert.Equal(ActivityPriority.Low, posted.Priority);
        Assert.Equal("14:31", Assert.IsType<ClockContent>(posted.Content).TimeText);
    }

    [Fact]
    public async Task Updates_on_minute_boundary_only()
    {
        IslandActivity? posted = null;
        using var subscription = _bus.Subscribe<ActivityPostedEvent>(e => posted = e.Activity);
        using var module = new ClockModule(_bus, _ui, _time, _shortcuts, new GlanceRegistry());
        await module.StartAsync(CancellationToken.None);
        var content = Assert.IsType<ClockContent>(posted!.Content);

        _time.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal("14:31", content.TimeText);
        Assert.Equal(0, _ui.Posted);

        _time.Advance(TimeSpan.FromSeconds(1.1));
        Assert.Equal("14:32", content.TimeText);
        Assert.Equal(1, _ui.Posted);

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal("14:33", content.TimeText);
        Assert.Equal(2, _ui.Posted);
    }

    [Fact]
    public async Task Stop_cancels_updates()
    {
        using var module = new ClockModule(_bus, _ui, _time, _shortcuts, new GlanceRegistry());
        await module.StartAsync(CancellationToken.None);

        await module.StopAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(0, _ui.Posted);
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public int Posted { get; private set; }

        public void Post(Action action)
        {
            Posted++;
            action();
        }
    }
}
