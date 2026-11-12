using Dynasland.Modules.Battery;
using Dynasland.Modules.Bluetooth;
using Dynasland.Modules.Brightness;
using Dynasland.Modules.Capture;
using Dynasland.Modules.Clipboard;
using Dynasland.Modules.Clock;
using Dynasland.Modules.Common;
using Dynasland.Modules.Downloads;
using Dynasland.Modules.Gaming;
using Dynasland.Modules.Life;
using Dynasland.Modules.Media;
using Dynasland.Modules.Notifications;
using Dynasland.Modules.SystemMonitor;
using Dynasland.Modules.Timer;
using Dynasland.Modules.Volume;
using Dynasland.Modules.Water;

namespace Dynasland.Tests.Modules;

public class PageIconsTests
{
    public static TheoryData<string> KnownModules =>
    [
        MediaModule.ModuleId,
        VolumeModule.ModuleId,
        BrightnessModule.ModuleId,
        TimerModule.ModuleId,
        WeatherModule.ModuleId,
        CalendarModule.ModuleId,
        DownloadsModule.ModuleId,
        BluetoothModule.ModuleId,
        BatteryModule.ModuleId,
        CaptureModule.ModuleId,
        ClipboardModule.ModuleId,
        NotificationsModule.ModuleId,
        SystemMonitorModule.ModuleId,
        GamingModule.ModuleId,
        ClockModule.ModuleId,
        WaterModule.ModuleId,
        PageIcons.AppModuleId,
    ];

    [Theory]
    [MemberData(nameof(KnownModules))]
    public void Every_module_that_shows_pages_has_an_icon_and_a_name(string moduleId)
    {
        var icon = PageIcons.For(moduleId);

        Assert.NotNull(icon);
        Assert.False(string.IsNullOrWhiteSpace(icon.Glyph));
        Assert.False(string.IsNullOrWhiteSpace(icon.Name));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_module_has_no_icon_so_the_bar_falls_back_to_dots(string? moduleId) => Assert.Null(PageIcons.For(moduleId));

    [Fact]
    public void Every_icon_resolves_to_a_drawable_vector()
    {
        foreach (var row in KnownModules)
        {
            Assert.NotNull(VectorIcons.Get(PageIcons.For(row)?.Glyph));
        }
    }

    [Fact]
    public void Uses_outline_vector_icons()
    {
        Assert.Equal(VectorIcons.Music, PageIcons.For(MediaModule.ModuleId)?.Glyph);
        Assert.Equal(VectorIcons.Volume, PageIcons.For(VolumeModule.ModuleId)?.Glyph);
        Assert.Equal(VectorIcons.Timer, PageIcons.For(TimerModule.ModuleId)?.Glyph);
        Assert.Equal(VectorIcons.Camera, PageIcons.For(CaptureModule.ModuleId)?.Glyph);
        Assert.Equal(VectorIcons.Game, PageIcons.For(GamingModule.ModuleId)?.Glyph);
    }

    [Fact]
    public void Water_uses_its_vector_icon()
    {
        var glyph = PageIcons.For(WaterModule.ModuleId)?.Glyph;

        Assert.Equal(VectorIcons.Water, glyph);
        Assert.NotNull(VectorIcons.Get(glyph));
    }
}
