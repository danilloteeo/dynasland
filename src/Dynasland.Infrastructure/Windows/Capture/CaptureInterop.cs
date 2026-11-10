using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace Dynasland.Infrastructure.Windows.Capture;

/// <summary>
/// Interoperabilidade para Windows.Graphics.Capture: dispositivo Direct3D 11 no formato WinRT e
/// itens de captura a partir de um monitor (HMONITOR) ou de uma janela (HWND).
/// </summary>
internal static class CaptureInterop
{
    private const int D3DDriverTypeHardware = 1;
    private const int D3DDriverTypeWarp = 5;
    private const uint D3D11CreateDeviceBgraSupport = 0x20;
    private const uint D3D11SdkVersion = 7;
    private const uint MonitorDefaultToPrimary = 1;

    private static readonly Guid DxgiDeviceIid = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid GraphicsCaptureItemInteropIid = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

    /// <summary>Dispositivo de hardware; sem GPU compatível, cai para o renderizador de software (WARP).</summary>
    public static IDirect3DDevice CreateDevice()
    {
        var hr = D3D11CreateDevice(IntPtr.Zero, D3DDriverTypeHardware, IntPtr.Zero, D3D11CreateDeviceBgraSupport, IntPtr.Zero, 0, D3D11SdkVersion, out var device, out _, out var context);
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(IntPtr.Zero, D3DDriverTypeWarp, IntPtr.Zero, D3D11CreateDeviceBgraSupport, IntPtr.Zero, 0, D3D11SdkVersion, out device, out _, out context));
        }

        try
        {
            var iid = DxgiDeviceIid;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(device, in iid, out var dxgiDevice));
            try
            {
                Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var inspectable));
                try
                {
                    return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
                }
                finally
                {
                    Marshal.Release(inspectable);
                }
            }
            finally
            {
                Marshal.Release(dxgiDevice);
            }
        }
        finally
        {
            Marshal.Release(context);
            Marshal.Release(device);
        }
    }

    public static GraphicsCaptureItem CreateItemForMonitor(IntPtr monitor) => CreateItem((interop, iid) => interop.CreateForMonitor(monitor, in iid));

    public static GraphicsCaptureItem CreateItemForWindow(IntPtr window) => CreateItem((interop, iid) => interop.CreateForWindow(window, in iid));

    /// <summary>HMONITOR do monitor cujo canto superior esquerdo está no ponto (ou o principal).</summary>
    public static IntPtr MonitorAt(int x, int y) => MonitorFromPoint(new Point { X = x, Y = y }, MonitorDefaultToPrimary);

    private static GraphicsCaptureItem CreateItem(Func<IGraphicsCaptureItemInterop, Guid, IntPtr> create)
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out var hstring));
        try
        {
            var factoryIid = GraphicsCaptureItemInteropIid;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstring, ref factoryIid, out var interop));
            var item = create(interop, GraphicsCaptureItemIid);
            try
            {
                return GraphicsCaptureItem.FromAbi(item);
            }
            finally
            {
                Marshal.Release(item);
            }
        }
        finally
        {
            WindowsDeleteString(hstring);
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, in Guid iid);

        IntPtr CreateForMonitor(IntPtr monitor, in Guid iid);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr featureLevels, uint featureLevelCount, uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr immediateContext);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, int length, out IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IGraphicsCaptureItemInterop factory);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);
}
