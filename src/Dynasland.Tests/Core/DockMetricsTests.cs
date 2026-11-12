using Dynasland.App.ViewModels;

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
}
