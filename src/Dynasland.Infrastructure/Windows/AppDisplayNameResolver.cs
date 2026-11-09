using System.Collections.Concurrent;
using System.Diagnostics;
using Windows.ApplicationModel;

namespace Dynasland.Infrastructure.Windows;

/// <summary>Converte um AppUserModelId (ex.: <c>Spotify.exe</c>, <c>Microsoft.ZuneMusic_...!App</c>) em nome amigável.</summary>
internal static class AppDisplayNameResolver
{
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string Resolve(string appUserModelId) => Cache.GetOrAdd(appUserModelId, ResolveCore);

    private static string ResolveCore(string appUserModelId)
    {
        if (string.IsNullOrWhiteSpace(appUserModelId))
        {
            return "Mídia";
        }

        // Aplicativos empacotados: o próprio Windows informa o nome (API do Windows 10 2004+).
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            try
            {
                var name = AppInfo.GetFromAppUserModelId(appUserModelId)?.DisplayInfo?.DisplayName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            catch (Exception)
            {
                // Não é um aplicativo empacotado.
            }
        }

        // Aplicativos Win32 (ex.: "Spotify.exe"): descrição do executável em execução.
        var executable = Path.GetFileNameWithoutExtension(appUserModelId.Split('!')[0]);
        try
        {
            foreach (var process in Process.GetProcessesByName(executable))
            {
                using (process)
                {
                    var description = process.MainModule?.FileVersionInfo.FileDescription;
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        return description;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Processo protegido ou já encerrado.
        }

        return string.IsNullOrEmpty(executable) ? appUserModelId : char.ToUpperInvariant(executable[0]) + executable[1..];
    }
}
