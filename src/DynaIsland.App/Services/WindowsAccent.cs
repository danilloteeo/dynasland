using System.IO;
using DynaIsland.Core.Services;
using Microsoft.Win32;

namespace DynaIsland.App.Services;

/// <summary>Cor de destaque escolhida no Windows, já clareada para receber texto escuro.</summary>
public static class WindowsAccent
{
    private const string DwmKey = @"Software\Microsoft\Windows\DWM";

    /// <summary>#RRGGBB ou <c>null</c> se o registro não tiver a cor.</summary>
    public static string? Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DwmKey);
            if (key?.GetValue("AccentColor") is not int abgr)
            {
                return null;
            }

            // O DWM guarda a cor como 0xAABBGGRR.
            return AccentColors.EnsureReadable((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
