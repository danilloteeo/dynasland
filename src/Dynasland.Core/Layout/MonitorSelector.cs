using Dynasland.Core.Models;

namespace Dynasland.Core.Layout;

public static class MonitorSelector
{
    /// <summary>
    /// Escolhe o monitor preferido; se ele foi desconectado, recorre ao principal
    /// e, por último, ao primeiro disponível.
    /// </summary>
    public static DisplayMonitor? Select(IReadOnlyList<DisplayMonitor> monitors, string? preferredDeviceName)
    {
        if (!string.IsNullOrWhiteSpace(preferredDeviceName))
        {
            var preferred = monitors.FirstOrDefault(m =>
                string.Equals(m.DeviceName, preferredDeviceName, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
    }
}
