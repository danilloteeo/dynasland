using Dynasland.Infrastructure.Windows;

namespace Dynasland.Tests.Infrastructure;

/// <summary>Integração real com o Win32: requer uma sessão de desktop interativa.</summary>
public class MonitorServiceTests
{
    [Fact]
    public void Enumerates_connected_monitors_with_one_primary()
    {
        var monitors = new MonitorService().GetMonitors();

        Assert.NotEmpty(monitors);
        Assert.Single(monitors, m => m.IsPrimary);
        Assert.All(monitors, m =>
        {
            Assert.StartsWith(@"\\.\DISPLAY", m.DeviceName, StringComparison.OrdinalIgnoreCase);
            Assert.True(m.Bounds.Width > 0 && m.Bounds.Height > 0);
            Assert.True(m.WorkArea.Width <= m.Bounds.Width);
            Assert.True(m.Dpi >= 96);
        });
    }

    [Fact]
    public void Primary_monitor_starts_at_origin()
    {
        var primary = new MonitorService().GetMonitors().Single(m => m.IsPrimary);

        Assert.Equal(0, primary.Bounds.Left);
        Assert.Equal(0, primary.Bounds.Top);
    }
}
