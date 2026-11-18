using DynaIsland.Core.Models;
using DynaIsland.Core.Modules;
using DynaIsland.Modules.Clock;
using DynaIsland.Modules.Life;
using DynaIsland.Modules.Media;
using Microsoft.Extensions.Time.Testing;

namespace DynaIsland.Tests.Modules;

public class IdleAndTintTests : IDisposable
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 9, 7, 5, 0, TimeSpan.Zero);

    private readonly ActivityRecorder _recorder = new();

    public void Dispose() => _recorder.Dispose();

    [Fact]
    public void Idle_shows_the_date_by_default()
    {
        var content = new ClockContent();
        content.Update(Monday);

        Assert.True(content.IsIdleVisible);
        Assert.False(content.IsIdleTimeOnly);
        Assert.False(content.HasIdleGlyph);
        Assert.Equal("seg, 9 nov", content.IdleText);
    }

    [Fact]
    public void Idle_shows_the_chosen_glance_in_its_short_form()
    {
        var content = new ClockContent();
        content.Update(Monday);
        IslandGlance[] glances =
        [
            new(WeatherModule.GlanceId, "W", "23° Nublado · São Paulo", ShortText: "23° Nublado"),
            new(CalendarModule.GlanceId, "C", "Dentista · hoje às 15:00"),
        ];

        content.SetIdle(IdleDisplay.Weather, glances);
        Assert.Equal("23° Nublado", content.IdleText);
        Assert.Equal("W", content.IdleGlyph);

        content.SetIdle(IdleDisplay.NextEvent, glances);
        Assert.Equal("Dentista · hoje às 15:00", content.IdleText);
        Assert.True(content.HasIdleGlyph);
    }

    [Fact]
    public void Idle_falls_back_to_the_date_when_the_glance_is_missing()
    {
        var content = new ClockContent();
        content.Update(Monday);

        content.SetIdle(IdleDisplay.Weather, []);

        Assert.Equal("seg, 9 nov", content.IdleText);
        Assert.False(content.HasIdleGlyph);
    }

    [Fact]
    public void Idle_can_be_time_only_or_hidden()
    {
        var content = new ClockContent();

        content.SetIdle(IdleDisplay.TimeOnly, []);
        Assert.True(content.IsIdleTimeOnly);
        Assert.True(content.IsIdleVisible);

        content.SetIdle(IdleDisplay.Nothing, []);
        Assert.False(content.IsIdleVisible);
    }
}
