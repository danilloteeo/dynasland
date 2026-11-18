using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DynaIsland.App.Services;

/// <summary>
/// Fundo de vidro do Windows 11 (acrílico do sistema) numa janela comum: estende o quadro do DWM para a área
/// cliente, deixa o WPF pintar transparente e pede o acrílico. Sem suporte, nada muda e a janela usa o fundo dela.
/// </summary>
public static class WindowBackdrop
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtTransientWindow = 3;

    /// <summary>O tipo de fundo do sistema existe a partir do Windows 11 22H2 (build 22621).</summary>
    public static bool IsSupported => Environment.OSVersion.Version.Build >= 22621;

    /// <summary>Liga o acrílico (agora ou quando o HWND existir). Devolve false sem mexer na janela se não houver suporte.</summary>
    public static bool TryApply(Window window)
    {
        if (!IsSupported)
        {
            return false;
        }

        window.Background = Brushes.Transparent;
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            Apply(window);
        }
        else
        {
            window.SourceInitialized += (_, _) => Apply(window);
        }

        return true;
    }

    private static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (HwndSource.FromHwnd(hwnd) is { } source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
        var type = DwmsbtTransientWindow;
        DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref type, sizeof(int));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
