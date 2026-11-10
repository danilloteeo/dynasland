using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Dynasland.Infrastructure.Windows.Capture;

/// <summary>
/// Capturas de tela com GDI (BitBlt da área de trabalho e PrintWindow com conteúdo renderizado por GPU
/// para janelas), codificadas em PNG pelo Windows Imaging Component.
/// </summary>
/// <remarks>O processo é DPI PerMonitorV2: as coordenadas e tamanhos são pixels físicos.</remarks>
public sealed class ScreenshotService(IMonitorService monitors) : IScreenshotService
{
    private const int SrcCopy = 0x00CC0020;
    private const int CaptureBlt = 0x40000000;
    private const uint PwRenderFullContent = 0x2;
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;

    public Task<CapturedImage> CaptureScreenAsync(string? monitorDeviceName = null) => Task.Run(() =>
    {
        var all = monitors.GetMonitors();
        var target = all.FirstOrDefault(m => string.Equals(m.DeviceName, monitorDeviceName, StringComparison.OrdinalIgnoreCase));
        var bounds = target?.Bounds ?? Union(all.Select(m => m.Bounds));
        return Encode(CopyFromScreen(bounds), bounds.Width, bounds.Height);
    });

    public Task<CapturedImage> CaptureRegionAsync(PixelRect region) => Task.Run(() =>
    {
        if (region.Width < 1 || region.Height < 1)
        {
            throw new ArgumentException("Área vazia.", nameof(region));
        }

        return Encode(CopyFromScreen(region), region.Width, region.Height);
    });

    public Task<CapturedImage> CaptureWindowAsync(IntPtr window) => Task.Run(() =>
    {
        if (!IsWindow(window))
        {
            throw new InvalidOperationException("A janela não existe mais.");
        }

        if (DwmGetWindowAttribute(window, DwmwaExtendedFrameBounds, out Rect frame, Marshal.SizeOf<Rect>()) != 0)
        {
            GetWindowRect(window, out frame);
        }

        GetWindowRect(window, out var full);
        var width = full.Right - full.Left;
        var height = full.Bottom - full.Top;
        if (width < 1 || height < 1)
        {
            throw new InvalidOperationException("A janela está minimizada.");
        }

        // PrintWindow desenha a janela inteira (com a borda invisível de redimensionamento); recorta para o quadro visível.
        var pixels = PrintWindowPixels(window, width, height);
        var crop = new PixelRect(frame.Left - full.Left, frame.Top - full.Top, frame.Right - full.Left, frame.Bottom - full.Top);
        if (crop.Left < 0 || crop.Top < 0 || crop.Right > width || crop.Bottom > height || crop.Width < 1 || crop.Height < 1)
        {
            return Encode(pixels, width, height);
        }

        return Encode(Crop(pixels, width, crop), crop.Width, crop.Height);
    });

    public IntPtr GetForegroundWindow() => NativeGetForegroundWindow();

    public IReadOnlyList<CaptureWindowInfo> GetWindows()
    {
        var windows = new List<CaptureWindowInfo>();
        var self = Environment.ProcessId;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindowTextLength(hwnd) == 0 || IsCloaked(hwnd))
            {
                return true;
            }

            GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == self)
            {
                return true;
            }

            var title = new StringBuilder(256);
            GetWindowText(hwnd, title, title.Capacity);
            string processName;
            try
            {
                processName = Process.GetProcessById((int)processId).ProcessName;
            }
            catch (ArgumentException)
            {
                return true;
            }

            windows.Add(new CaptureWindowInfo(hwnd, title.ToString(), processName));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static bool IsCloaked(IntPtr hwnd) =>
        DwmGetWindowAttribute(hwnd, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    private static PixelRect Union(IEnumerable<PixelRect> rects)
    {
        var list = rects.ToList();
        return list.Count == 0
            ? new PixelRect(0, 0, 1, 1)
            : new PixelRect(list.Min(r => r.Left), list.Min(r => r.Top), list.Max(r => r.Right), list.Max(r => r.Bottom));
    }

    private static byte[] CopyFromScreen(PixelRect area)
    {
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, area.Width, area.Height);
        var previous = SelectObject(memory, bitmap);
        try
        {
            if (!BitBlt(memory, 0, 0, area.Width, area.Height, screen, area.Left, area.Top, SrcCopy | CaptureBlt))
            {
                throw new InvalidOperationException($"Falha na captura da tela (erro {Marshal.GetLastWin32Error()}).");
            }

            SelectObject(memory, previous);
            return ReadPixels(memory, bitmap, area.Width, area.Height);
        }
        finally
        {
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    private static byte[] PrintWindowPixels(IntPtr window, int width, int height)
    {
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height);
        var previous = SelectObject(memory, bitmap);
        try
        {
            if (!PrintWindow(window, memory, PwRenderFullContent))
            {
                throw new InvalidOperationException("O Windows não permitiu capturar esta janela.");
            }

            SelectObject(memory, previous);
            return ReadPixels(memory, bitmap, width, height);
        }
        finally
        {
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>Pixels BGRA de cima para baixo, alfa opaco (GDI não preenche o alfa).</summary>
    private static byte[] ReadPixels(IntPtr dc, IntPtr bitmap, int width, int height)
    {
        var info = new BitmapInfo
        {
            Size = 40, // BITMAPINFOHEADER (sem a tabela de cores)
            Width = width,
            Height = -height,
            Planes = 1,
            BitCount = 32,
        };
        var pixels = new byte[width * height * 4];
        if (GetDIBits(dc, bitmap, 0, (uint)height, pixels, ref info, 0) == 0)
        {
            throw new InvalidOperationException("Falha ao ler a imagem capturada.");
        }

        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
        }

        return pixels;
    }

    private static byte[] Crop(byte[] pixels, int width, PixelRect crop)
    {
        var result = new byte[crop.Width * crop.Height * 4];
        for (var y = 0; y < crop.Height; y++)
        {
            System.Buffer.BlockCopy(pixels, ((crop.Top + y) * width + crop.Left) * 4, result, y * crop.Width * 4, crop.Width * 4);
        }

        return result;
    }

    internal static CapturedImage Encode(byte[] bgra, int width, int height)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream).AsTask().GetAwaiter().GetResult();
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, 96, 96, bgra);
        encoder.FlushAsync().AsTask().GetAwaiter().GetResult();

        var png = new byte[stream.Size];
        stream.Seek(0);
        stream.ReadAsync(png.AsBuffer(), (uint)png.Length, InputStreamOptions.None).AsTask().GetAwaiter().GetResult();
        return new CapturedImage(png, width, height);
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
        public int Colors;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, int rop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, [Out] byte[] bits, ref BitmapInfo info, uint usage);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern IntPtr NativeGetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out Rect value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
}
