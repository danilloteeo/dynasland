using Microsoft.Extensions.Logging;

namespace DynaIsland.Infrastructure.Database;

public enum LegacyMigrationResult
{
    /// <summary>Não havia pasta antiga, ou a nova já tem banco.</summary>
    NotNeeded,

    Migrated,

    /// <summary>Não foi possível copiar; a pasta antiga continua intacta e deve seguir em uso.</summary>
    Failed,
}

/// <summary>
/// O aplicativo se chamava Dynasland até a 1.7.1 e guardava tudo em <c>%LocalAppData%\Dynasland</c>.
/// Na primeira execução com o nome novo, copia essa pasta para <c>%LocalAppData%\DynaIsland</c>
/// (o banco passa de <c>dynasland.db</c> para <c>dynaisland.db</c>). A pasta antiga nunca é alterada.
/// </summary>
public static class LegacyDataMigration
{
    private const string LegacyDatabaseName = "dynasland.db";
    private const string DatabaseName = "dynaisland.db";

    /// <summary>Cache do WebView2: é recriado sozinho e pode estar travado por outro processo.</summary>
    private static readonly string[] SkippedFolders = ["webview2"];

    public static LegacyMigrationResult Run(string directory, string legacyDirectory, ILogger? logger = null)
    {
        if (!Directory.Exists(legacyDirectory) || File.Exists(Path.Combine(directory, DatabaseName)))
        {
            return LegacyMigrationResult.NotNeeded;
        }

        var staging = directory + ".migrando";
        try
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            Copy(legacyDirectory, staging);
            RenameDatabaseFiles(staging);

            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }

            Directory.Move(staging, directory);
            logger?.LogInformation("Dados migrados de {Legacy} para {Directory}.", legacyDirectory, directory);
            return LegacyMigrationResult.Migrated;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogError(ex, "Não foi possível migrar os dados de {Legacy}; a pasta antiga continua em uso.", legacyDirectory);
            TryDelete(staging);
            return LegacyMigrationResult.Failed;
        }
    }

    private static void Copy(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var folder in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(folder);
            if (!SkippedFolders.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                Copy(folder, Path.Combine(target, name));
            }
        }
    }

    /// <summary>dynasland.db, dynasland.db-wal, dynasland.db-shm e cópias de segurança passam ao nome novo.</summary>
    private static void RenameDatabaseFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, LegacyDatabaseName + "*"))
        {
            var name = Path.GetFileName(file);
            File.Move(file, Path.Combine(directory, DatabaseName + name[LegacyDatabaseName.Length..]));
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Restos de uma tentativa que falhou: a próxima execução recomeça do zero.
        }
    }
}
