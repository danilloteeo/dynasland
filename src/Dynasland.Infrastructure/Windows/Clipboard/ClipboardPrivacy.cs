using System.Runtime.InteropServices;

namespace Dynasland.Infrastructure.Windows.Clipboard;

/// <summary>
/// Formatos que aplicativos (gerenciadores de senha, por exemplo) usam para pedir que o conteúdo
/// copiado não seja exibido nem guardado por monitores e pelo histórico do Windows.
/// </summary>
internal static class ClipboardPrivacy
{
    private static readonly uint ExcludeFromMonitoring = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint ViewerIgnore = RegisterClipboardFormat("Clipboard Viewer Ignore");
    private static readonly uint CanIncludeInHistory = RegisterClipboardFormat("CanIncludeInClipboardHistory");

    /// <summary><c>true</c> se o conteúdo atual pediu para não ser monitorado.</summary>
    public static bool IsPrivate(IntPtr hwnd)
    {
        if (IsClipboardFormatAvailable(ExcludeFromMonitoring) || IsClipboardFormatAvailable(ViewerIgnore))
        {
            return true;
        }

        return IsClipboardFormatAvailable(CanIncludeInHistory) && ReadDword(hwnd, CanIncludeInHistory) == 0;
    }

    private static int? ReadDword(IntPtr hwnd, uint format)
    {
        // Outro processo pode estar com a área de transferência aberta; tenta algumas vezes.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(hwnd))
            {
                try
                {
                    var handle = GetClipboardData(format);
                    var pointer = handle == IntPtr.Zero ? IntPtr.Zero : GlobalLock(handle);
                    if (pointer == IntPtr.Zero)
                    {
                        return null;
                    }

                    try
                    {
                        return Marshal.ReadInt32(pointer);
                    }
                    finally
                    {
                        GlobalUnlock(handle);
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }

            Thread.Sleep(20);
        }

        // Na dúvida, trata como privado.
        return 0;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string format);

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr handle);
}
