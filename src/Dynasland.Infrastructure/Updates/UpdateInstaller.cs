using System.Diagnostics;
using System.Security.Cryptography;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Updates;

/// <param name="Folder">Onde ficam o instalador baixado, o registro da instalação e o marcador de atualização.</param>
/// <param name="AppDirectory">Pasta do executável em uso.</param>
public sealed record UpdateInstallerOptions(string Folder, string AppDirectory)
{
    /// <summary>%LocalAppData%\Dynasland\updates, ao lado do banco de dados.</summary>
    public static UpdateInstallerOptions Default(string databasePath) =>
        new(Path.Combine(Path.GetDirectoryName(databasePath)!, "updates"), AppContext.BaseDirectory);
}

/// <summary>
/// Baixa o instalador da versão (somente de github.com, por HTTPS), confere tamanho e hash e o executa
/// em modo silencioso com <c>/RELAUNCH</c>, que reabre o aplicativo ao terminar.
/// </summary>
public sealed class UpdateInstaller(HttpClient http, UpdateInstallerOptions options, ILogger<UpdateInstaller> logger) : IUpdateInstaller
{
    /// <summary>Criado pelo Inno Setup na pasta de instalação; a versão portátil não tem.</summary>
    public const string UninstallerName = "unins000.exe";

    private const string MarkerName = "pending.txt";
    private const string LogName = "install.log";

    public bool CanInstall => File.Exists(Path.Combine(options.AppDirectory, UninstallerName));

    public async Task<string> DownloadAsync(UpdateAsset asset, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdateVerificationException("Endereço de download inesperado.");
        }

        Directory.CreateDirectory(options.Folder);
        DeleteInstallers();

        // Só o nome do arquivo: a publicação não escolhe a pasta de destino.
        var target = Path.Combine(options.Folder, Path.GetFileName(asset.Name));
        var partial = target + ".partial";
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            string hash;
            long total = 0;
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > asset.Size)
                    {
                        throw new UpdateVerificationException("O arquivo baixado é maior que o publicado.");
                    }

                    sha.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    progress?.Report((double)total / asset.Size);
                }

                hash = Convert.ToHexStringLower(sha.GetHashAndReset());
            }

            if (total != asset.Size)
            {
                throw new UpdateVerificationException("O arquivo baixado veio incompleto.");
            }

            if (asset.Sha256 is { } expected && !expected.Equals(hash, StringComparison.OrdinalIgnoreCase))
            {
                throw new UpdateVerificationException("O arquivo baixado não confere com o publicado.");
            }

            File.Move(partial, target, overwrite: true);
            logger.LogInformation("Instalador {Name} baixado e conferido ({Hash}).", asset.Name, hash);
            return target;
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    public void Launch(string installerPath, string version)
    {
        File.WriteAllText(Path.Combine(options.Folder, MarkerName), version);
        var log = Path.Combine(options.Folder, LogName);
        var start = new ProcessStartInfo(installerPath) { UseShellExecute = false };
        foreach (var argument in (string[])["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/RELAUNCH", $"/LOG={log}"])
        {
            start.ArgumentList.Add(argument);
        }

        Process.Start(start)?.Dispose();
        logger.LogInformation("Instalador da versão {Version} iniciado.", version);
    }

    public string? ConsumeCompletedUpdate(Version currentVersion)
    {
        var marker = Path.Combine(options.Folder, MarkerName);
        try
        {
            if (!File.Exists(marker))
            {
                return null;
            }

            var pending = File.ReadAllText(marker).Trim();
            File.Delete(marker);
            DeleteInstallers();

            if (Version.TryParse(pending, out var target) && currentVersion >= target)
            {
                return pending;
            }

            logger.LogWarning("A atualização para {Version} não foi concluída; veja {Log}.", pending, Path.Combine(options.Folder, LogName));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Falha ao ler o marcador de atualização.");
            return null;
        }
    }

    private void DeleteInstallers()
    {
        if (!Directory.Exists(options.Folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(options.Folder).Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)))
        {
            TryDelete(file);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Arquivo em uso: fica para a próxima limpeza.
        }
    }
}
