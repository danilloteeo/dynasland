using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Tests.Core;

public class IslandPlaceOnEdgeTests
{
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);

    [Theory]
    [InlineData(IslandPosition.Center, 720)]
    [InlineData(IslandPosition.Left, 0)]
    [InlineData(IslandPosition.Right, 1440)]
    public void Bottom_edge_sits_above_the_work_area_bottom(IslandPosition position, int expectedX)
    {
        var point = IslandPlacementCalculator.PlaceOnEdge(WorkArea, 480, 460, position, IslandEdge.Bottom, margin: 8);

        Assert.Equal(new PixelPoint(expectedX, 1040 - 460 - 8), point);
    }

    [Fact]
    public void Top_edge_uses_the_margin_from_the_top()
    {
        var point = IslandPlacementCalculator.PlaceOnEdge(WorkArea, 480, 460, IslandPosition.Center, IslandEdge.Top, margin: 12);

        Assert.Equal(new PixelPoint(720, 12), point);
    }

    [Fact]
    public void Work_area_with_a_taskbar_at_the_top_is_respected()
    {
        var area = new PixelRect(0, 48, 1920, 1080);

        var top = IslandPlacementCalculator.PlaceOnEdge(area, 480, 460, IslandPosition.Center, IslandEdge.Top, 0);
        var bottom = IslandPlacementCalculator.PlaceOnEdge(area, 480, 460, IslandPosition.Center, IslandEdge.Bottom, 0);

        Assert.Equal(48, top.Y);
        Assert.Equal(1080 - 460, bottom.Y);
    }

    [Fact]
    public void Negative_margin_is_treated_as_zero()
    {
        var point = IslandPlacementCalculator.PlaceOnEdge(WorkArea, 480, 460, IslandPosition.Center, IslandEdge.Bottom, -20);

        Assert.Equal(1040 - 460, point.Y);
    }
}
