using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Tests.Core;

public class IslandLayoutTests
{
    [Fact]
    public void States_grow_from_compact_to_interactive()
    {
        Assert.True(IslandLayout.Compact.Width < IslandLayout.Expanded.Width);
        Assert.True(IslandLayout.Expanded.Width < IslandLayout.Interactive.Width);
        Assert.True(IslandLayout.Compact.Height < IslandLayout.Expanded.Height);
        Assert.True(IslandLayout.Expanded.Height < IslandLayout.Interactive.Height);
    }

    [Theory]
    [InlineData(IslandState.Compact)]
    [InlineData(IslandState.Expanded)]
    [InlineData(IslandState.Interactive)]
    public void Corner_radius_never_exceeds_half_height(IslandState state)
    {
        var dimensions = IslandLayout.For(state);
        Assert.True(dimensions.CornerRadius <= dimensions.Height / 2);
    }

    [Fact]
    public void Compact_state_is_a_full_pill()
    {
        Assert.Equal(IslandLayout.Compact.Height / 2, IslandLayout.Compact.CornerRadius);
    }
}
