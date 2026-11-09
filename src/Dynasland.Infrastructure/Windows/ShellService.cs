using System.Diagnostics;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Windows;

public sealed class ShellService(ILogger<ShellService> logger) : IShellService
{
    public bool Open(string path) => Start(new ProcessStartInfo(path) { UseShellExecute = true }, path);

    public bool RevealInFolder(string path) =>
        Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }, path);

    public bool OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível abrir {Url}.", url);
            return false;
        }
    }

    private bool Start(ProcessStartInfo startInfo, string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            logger.LogInformation("{Path} não existe mais.", path);
            return false;
        }

        try
        {
            Process.Start(startInfo)?.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível abrir {Path}.", path);
            return false;
        }
    }
}
