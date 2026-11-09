using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Tests.Core;

public class IslandPlacementCalculatorTests
{
    [Fact]
    public void Centers_on_full_hd_primary_monitor()
    {
        var workArea = new PixelRect(0, 0, 1920, 1040);

        var position = IslandPlacementCalculator.TopCenter(workArea, windowWidth: 460, topMargin: 8);

        Assert.Equal(new PixelPoint(730, 8), position);
    }

    [Fact]
    public void Centers_on_secondary_monitor_left_of_primary()
    {
        var workArea = new PixelRect(-2560, 0, 0, 1400);

        var position = IslandPlacementCalculator.TopCenter(workArea, windowWidth: 690, topMargin: 12);

        Assert.Equal(new PixelPoint(-1625, 12), position);
    }

    [Fact]
    public void Respects_taskbar_docked_at_top()
    {
        var workArea = new PixelRect(0, 48, 3840, 2160);

        var position = IslandPlacementCalculator.TopCenter(workArea, windowWidth: 920, topMargin: 16);

        Assert.Equal(new PixelPoint(1460, 64), position);
    }

    [Fact]
    public void Negative_margin_is_treated_as_zero()
    {
        var position = IslandPlacementCalculator.TopCenter(new PixelRect(0, 0, 1000, 800), 200, -10);

        Assert.Equal(0, position.Y);
    }
}
