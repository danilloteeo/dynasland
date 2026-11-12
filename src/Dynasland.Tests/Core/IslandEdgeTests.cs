using Dynasland.Core.Models;
using Dynasland.Core.Services;

namespace Dynasland.Tests.Core;

public class IslandEdgeTests
{
    [Fact]
    public void Default_edge_is_bottom()
    {
        Assert.Equal(IslandEdge.Bottom, IslandSettings.Default.Edge);
    }

    [Fact]
    public void Edge_round_trips_through_the_mapper()
    {
        var values = IslandSettingsMapper.ToValues(IslandSettings.Default with { Edge = IslandEdge.Top });

        Assert.Equal("Top", values[IslandSettingsMapper.Keys.Edge]);
        Assert.Equal(IslandEdge.Top, IslandSettingsMapper.FromValues(values).Edge);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Left")]
    [InlineData("lixo")]
    public void Invalid_edge_falls_back_to_the_default(string raw)
    {
        var values = new Dictionary<string, string> { [IslandSettingsMapper.Keys.Edge] = raw };

        Assert.Equal(IslandEdge.Bottom, IslandSettingsMapper.FromValues(values).Edge);
    }
}
