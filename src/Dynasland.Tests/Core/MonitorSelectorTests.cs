using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Tests.Core;

public class MonitorSelectorTests
{
    private static readonly DisplayMonitor Primary = new(
        @"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), IsPrimary: true, Dpi: 96);

    private static readonly DisplayMonitor Secondary = new(
        @"\\.\DISPLAY2", new PixelRect(1920, 0, 4480, 1440), new PixelRect(1920, 0, 4480, 1440), IsPrimary: false, Dpi: 144);

    [Fact]
    public void Returns_preferred_monitor_when_connected()
    {
        var selected = MonitorSelector.Select([Primary, Secondary], @"\\.\display2");

        Assert.Same(Secondary, selected);
    }

    [Fact]
    public void Falls_back_to_primary_when_preferred_is_disconnected()
    {
        var selected = MonitorSelector.Select([Secondary, Primary], @"\\.\DISPLAY7");

        Assert.Same(Primary, selected);
    }

    [Fact]
    public void Uses_primary_when_no_preference()
    {
        Assert.Same(Primary, MonitorSelector.Select([Secondary, Primary], null));
    }

    [Fact]
    public void Returns_null_without_monitors()
    {
        Assert.Null(MonitorSelector.Select([], null));
    }

    [Fact]
    public void Exposes_scale_and_display_number()
    {
        Assert.Equal(1.5, Secondary.Scale);
        Assert.Equal(2, Secondary.DisplayNumber);
    }
}
