using Dynasland.Core.Models;
using static Dynasland.Infrastructure.Windows.Interop.NativeMethods;

namespace Dynasland.Infrastructure.Windows;

/// <summary>Operações Win32 sobre janelas de sobreposição (a ilha).</summary>
public static class WindowInterop
{
    /// <summary>
    /// Torna a janela uma ferramenta que nunca recebe foco:
    /// WS_EX_TOOLWINDOW remove da barra de tarefas e do Alt+Tab;
    /// WS_EX_NOACTIVATE impede que um clique roube o foco do aplicativo em uso.
    /// </summary>
    public static void ApplyOverlayStyles(IntPtr hwnd)
    {
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        exStyle &= ~WS_EX_APPWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle);
    }

    public static PixelRect GetBounds(IntPtr hwnd) =>
        GetWindowRect(hwnd, out var rect)
            ? new PixelRect(rect.Left, rect.Top, rect.Right, rect.Bottom)
            : default;

    /// <summary>Move a janela (pixels físicos) mantendo-a acima das demais e sem ativá-la.</summary>
    public static void MoveTopmost(IntPtr hwnd, PixelPoint position) =>
        SetWindowPos(hwnd, HWND_TOPMOST, position.X, position.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
}
