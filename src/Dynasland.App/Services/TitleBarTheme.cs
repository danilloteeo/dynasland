using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Dynasland.App.Services;

/// <summary>Barra de título clara ou escura (DWM), coerente com o tema das janelas do Dynasland.</summary>
public static class TitleBarTheme
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint RdwInvalidate = 0x0001;
    private const uint RdwUpdateNow = 0x0100;
    private const uint RdwFrame = 0x0400;

    /// <summary>Aplica na janela (sem efeito antes de o HWND existir) e a mantém sincronizada com o tema.</summary>
    public static void Attach(Window window, ThemeService theme)
    {
        void Apply(object? sender, EventArgs e) => Set(window, theme.IsDark);

        window.SourceInitialized += Apply;
        theme.ThemeChanged += Apply;
        window.Closed += (_, _) => theme.ThemeChanged -= Apply;
    }

    public static void Set(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var value = dark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkModeBefore20H1, ref value, sizeof(int));
        }

        // O Windows só repinta a barra de título quando o quadro da janela é invalidado.
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, RdwFrame | RdwInvalidate | RdwUpdateNow);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hwnd, IntPtr updateRect, IntPtr region, uint flags);
}
