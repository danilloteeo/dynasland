using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Windows.Brightness;

/// <summary>
/// Brilho de monitores externos por DDC/CI (Monitor Configuration API do Windows, dxva2). Monitores
/// que não respondem ao DDC/CI (ou com ele desligado no menu do monitor) simplesmente não aparecem.
/// </summary>
/// <remarks>
/// As chamadas DDC/CI são lentas (dezenas de milissegundos, conversam com o monitor pelo cabo) e não
/// existe notificação de mudança: os valores são lidos quando o painel é aberto. Cada operação
/// enumera os monitores de novo, porque os handles físicos mudam quando a configuração de telas muda.
/// </remarks>
public sealed partial class DdcBrightnessService(ILogger<DdcBrightnessService> logger) : IExternalBrightnessService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task<IReadOnlyList<ExternalDisplayBrightness>> GetDisplaysAsync() => RunAsync<IReadOnlyList<ExternalDisplayBrightness>>(() =>
    {
        var result = new List<ExternalDisplayBrightness>();
        foreach (var monitor in Enumerate())
        {
            if (GetMonitorBrightness(monitor.Handle, out var min, out var current, out var max) && max > min)
            {
                result.Add(new ExternalDisplayBrightness(monitor.Id, monitor.Name, (int)Math.Round((current - min) * 100.0 / (max - min))));
            }
        }

        DestroyAll();
        return result;
    }, []);

    public Task<bool> SetAsync(string displayId, int percent) => RunAsync(() =>
    {
        var ok = false;
        foreach (var monitor in Enumerate().Where(m => m.Id == displayId))
        {
            if (GetMonitorBrightness(monitor.Handle, out var min, out _, out var max) && max > min)
            {
                var value = (uint)Math.Round(min + Math.Clamp(percent, 0, 100) * (max - min) / 100.0);
                ok = SetMonitorBrightness(monitor.Handle, value);
            }
        }

        DestroyAll();
        return ok;
    }, false);

    private readonly List<PhysicalMonitorHandle[]> _open = [];

    private async Task<T> RunAsync<T>(Func<T> work, T fallback)
    {
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(work);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha no controle de brilho DDC/CI.");
            return fallback;
        }
        finally
        {
            _gate.Release();
        }
    }

    private List<(string Id, string Name, IntPtr Handle)> Enumerate()
    {
        var monitors = new List<(string, string, IntPtr)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hmonitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            var device = GetMonitorInfo(hmonitor, ref info) ? info.DeviceName : "?";
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hmonitor, out var count) || count == 0)
            {
                return true;
            }

            var physical = new PhysicalMonitorHandle[count];
            if (!GetPhysicalMonitorsFromHMONITOR(hmonitor, count, physical))
            {
                return true;
            }

            _open.Add(physical);
            for (var i = 0; i < physical.Length; i++)
            {
                var number = DisplayNumber().Match(device) is { Success: true } m ? m.Groups[1].Value : device;
                var description = physical[i].Description?.Trim();
                var name = string.IsNullOrEmpty(description) || description.StartsWith("Generic", StringComparison.OrdinalIgnoreCase)
                    ? $"Monitor {number}"
                    : $"Monitor {number} · {description}";
                monitors.Add(($"{device}#{i}", name, physical[i].Handle));
            }

            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    private void DestroyAll()
    {
        foreach (var handles in _open)
        {
            DestroyPhysicalMonitors((uint)handles.Length, handles);
        }

        _open.Clear();
    }

    [GeneratedRegex(@"DISPLAY(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DisplayNumber();

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PhysicalMonitorHandle
    {
        public IntPtr Handle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitorHandle[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitors(uint count, PhysicalMonitorHandle[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorBrightness(IntPtr monitor, out uint minimum, out uint current, out uint maximum);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr monitor, uint brightness);
}
