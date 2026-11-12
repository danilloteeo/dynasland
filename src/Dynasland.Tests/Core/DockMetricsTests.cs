using Dynasland.App.ViewModels;
using Dynasland.Core.Models;

namespace Dynasland.Tests.Core;

public class DockMetricsTests
{
    [Fact]
    public void Width_grows_with_the_item_count()
    {
        Assert.True(DockMetrics.Width(6, hasActivePill: false) > DockMetrics.Width(3, hasActivePill: false));
    }

    [Fact]
    public void The_active_pill_adds_room_for_its_text()
    {
        Assert.Equal(DockMetrics.Width(4, false) + DockMetrics.ActivePillExtra, DockMetrics.Width(4, true));
    }

    [Fact]
    public void An_empty_dock_keeps_a_minimum_width()
    {
        Assert.Equal(DockMetrics.MinimumWidth, DockMetrics.Width(0, false));
    }

    [Fact]
    public void Idle_text_room_depends_on_the_chosen_idle_option()
    {
        Assert.Equal(0, DockMetrics.IdleExtra(IdleDisplay.Nothing));
        Assert.True(DockMetrics.IdleExtra(IdleDisplay.TimeOnly) < DockMetrics.IdleExtra(IdleDisplay.DateAndTime));
        Assert.True(DockMetrics.IdleExtra(IdleDisplay.DateAndTime) < DockMetrics.IdleExtra(IdleDisplay.NextEvent));
        Assert.All(Enum.GetValues<IdleDisplay>().Where(d => d != IdleDisplay.Nothing), d => Assert.True(DockMetrics.IdleExtra(d) > 0));
    }

    [Fact]
    public void Eight_icons_with_the_widest_idle_text_still_fit_the_island_window()
    {
        const double windowWidth = 600;
        var widest = Enum.GetValues<IdleDisplay>().Max(DockMetrics.IdleExtra);

        Assert.True(DockMetrics.Width(8, widest) <= windowWidth - 32);
    }
}
