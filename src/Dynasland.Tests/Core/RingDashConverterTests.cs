using Dynasland.App.Converters;

namespace Dynasland.Tests.Core;

public class RingDashConverterTests
{
    [Fact]
    public void Half_progress_fills_half_of_the_circumference()
    {
        var dashes = RingDashConverter.Dashes(0.5);

        Assert.Equal(RingDashConverter.CircumferenceUnits / 2, dashes[0], precision: 6);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    public void Progress_is_clamped_between_empty_and_full(double progress, double expectedShare)
    {
        var dashes = RingDashConverter.Dashes(progress);

        Assert.Equal(RingDashConverter.CircumferenceUnits * expectedShare, dashes[0], precision: 6);
    }

    [Fact]
    public void The_gap_is_longer_than_the_whole_ring_so_the_dash_does_not_repeat()
    {
        var dashes = RingDashConverter.Dashes(0.3);

        Assert.True(dashes[1] > RingDashConverter.CircumferenceUnits);
    }
}
