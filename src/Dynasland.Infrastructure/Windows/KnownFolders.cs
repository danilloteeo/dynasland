using System.Runtime.InteropServices;

namespace Dynasland.Infrastructure.Windows;

internal static class KnownFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    /// <summary>Pasta Downloads do usuário (respeita redirecionamento feito nas propriedades da pasta).</summary>
    public static string? GetDownloads()
    {
        if (SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero, out var pointer) != 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid folderId, uint flags, IntPtr token, out IntPtr path);
}
