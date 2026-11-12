using Dynasland.Modules.Notifications;

namespace Dynasland.Tests.Modules;

public class NotificationReadingTests
{
    [Theory]
    [InlineData("", 1)]
    [InlineData("oi", 1)]
    [InlineData("1234567890123456789012345678901234567890123456", 1)]
    [InlineData("12345678901234567890123456789012345678901234567", 2)]
    [InlineData("um texto bem longo que passa de uma linha e continua por mais uma linha inteira aqui", 2)]
    [InlineData("muito longo muito longo muito longo muito longo muito longo muito longo muito longo muito longo muito longo muito longo muito longo muito longo", 3)]
    public void Lines_grow_with_the_text_up_to_three(string body, int expected)
    {
        Assert.Equal(expected, NotificationReading.Lines(body));
    }

    [Theory]
    [InlineData(1, 72)]
    [InlineData(2, 92)]
    [InlineData(3, 112)]
    [InlineData(9, 112)]
    [InlineData(0, 72)]
    public void Height_follows_the_line_count(int lines, double expected)
    {
        Assert.Equal(expected, NotificationReading.Height(lines));
    }

    [Fact]
    public void Duration_adds_forty_milliseconds_per_character()
    {
        var duration = NotificationReading.Duration(TimeSpan.FromSeconds(6), 50, highPriority: false);

        Assert.Equal(8, duration.TotalSeconds, precision: 6);
    }

    [Fact]
    public void Duration_is_capped_at_twelve_seconds()
    {
        var duration = NotificationReading.Duration(TimeSpan.FromSeconds(6), 5000, highPriority: false);

        Assert.Equal(12, duration.TotalSeconds, precision: 6);
    }

    [Fact]
    public void A_base_longer_than_the_cap_is_kept()
    {
        var duration = NotificationReading.Duration(TimeSpan.FromSeconds(20), 100, highPriority: false);

        Assert.Equal(20, duration.TotalSeconds, precision: 6);
    }

    [Fact]
    public void High_priority_stays_two_seconds_longer()
    {
        var normal = NotificationReading.Duration(TimeSpan.FromSeconds(6), 50, false);
        var high = NotificationReading.Duration(TimeSpan.FromSeconds(6), 50, true);

        Assert.Equal(2, (high - normal).TotalSeconds, precision: 6);
    }
}
