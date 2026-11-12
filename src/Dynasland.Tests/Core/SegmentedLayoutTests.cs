using Dynasland.App.Controls;

namespace Dynasland.Tests.Core;

public class SegmentedLayoutTests
{
    [Fact]
    public void Pill_covers_the_selected_equal_width_segment_inside_the_padding()
    {
        // 3 segmentos, largura total 306, folga de 3 em cada lado => área útil 300, segmento 100.
        var (left, width) = SegmentedLayout.Pill(count: 3, index: 1, totalWidth: 306, padding: 3);

        Assert.Equal(103, left, precision: 6);
        Assert.Equal(100, width, precision: 6);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, -1)]
    [InlineData(3, 3)]
    public void Pill_is_hidden_when_nothing_is_selected_or_there_are_no_items(int count, int index)
    {
        var (_, width) = SegmentedLayout.Pill(count, index, totalWidth: 300, padding: 3);

        Assert.Equal(0, width);
    }
}
