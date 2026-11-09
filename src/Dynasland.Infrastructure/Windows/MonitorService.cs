using System.Runtime.InteropServices;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using static Dynasland.Infrastructure.Windows.Interop.NativeMethods;

namespace Dynasland.Infrastructure.Windows;

/// <summary>Enumera monitores via EnumDisplayMonitors/GetMonitorInfo e lê o DPI efetivo de cada um.</summary>
public sealed class MonitorService : IMonitorService
{
    private const uint DefaultDpi = 96;

    public IReadOnlyList<DisplayMonitor> GetMonitors()
    {
        var monitors = new List<DisplayMonitor>();

        bool Collect(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data)
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                monitors.Add(new DisplayMonitor(
                    info.szDevice,
                    ToPixelRect(info.rcMonitor),
                    ToPixelRect(info.rcWork),
                    (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                    GetEffectiveDpi(hMonitor)));
            }

            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Collect, IntPtr.Zero);

        return monitors
            .OrderBy(m => m.Bounds.Left)
            .ThenBy(m => m.Bounds.Top)
            .ToList();
    }

    private static uint GetEffectiveDpi(IntPtr hMonitor) =>
        GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0 ? dpiX : DefaultDpi;

    private static PixelRect ToPixelRect(RECT rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);
}
