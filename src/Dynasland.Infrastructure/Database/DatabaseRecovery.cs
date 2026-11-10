using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Database;

/// <summary>
/// Prepara o banco na inicialização. Se o arquivo estiver corrompido (ou não for um banco SQLite),
/// move-o para <c>dynasland.db.corrompido-AAAAMMDD-HHMMSS</c> e recria do zero com as configurações
/// padrão, em vez de impedir o aplicativo de abrir.
/// </summary>
public sealed class DatabaseRecovery(
    DatabaseOptions options,
    MigrationRunner migrations,
    ILogger<DatabaseRecovery> logger)
{
    private const int SqliteCorrupt = 11;
    private const int SqliteNotADatabase = 26;

    /// <returns>Caminho da cópia do banco corrompido, quando houve recuperação; senão <c>null</c>.</returns>
    public async Task<string?> MigrateOrRecoverAsync(CancellationToken cancellationToken = default)
    {
        string? backup = null;
        if (File.Exists(options.DatabasePath) && !await IsHealthyAsync(cancellationToken))
        {
            backup = MoveAside(options.DatabasePath);
        }

        try
        {
            await migrations.MigrateAsync(cancellationToken);
        }
        catch (SqliteException ex) when (backup is null && IsCorruption(ex))
        {
            logger.LogError(ex, "Banco de dados corrompido durante a migração.");
            backup = MoveAside(options.DatabasePath);
            await migrations.MigrateAsync(cancellationToken);
        }

        return backup;
    }

    internal static bool IsCorruption(SqliteException ex) =>
        ex.SqliteErrorCode is SqliteCorrupt or SqliteNotADatabase;

    /// <summary>
    /// <c>quick_check</c> antes das migrações: páginas danificadas podem não impedir a abertura do arquivo,
    /// mas fazem as tabelas parecerem vazias ou ausentes.
    /// </summary>
    private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = options.DatabasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
            }.ToString());
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            if (string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            logger.LogError("Banco de dados corrompido ({Result}); recriando com as configurações padrão.", result);
            return false;
        }
        catch (SqliteException ex) when (IsCorruption(ex))
        {
            logger.LogError(ex, "Banco de dados corrompido; recriando com as configurações padrão.");
            return false;
        }
    }

    private static string MoveAside(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        var suffix = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var backup = $"{databasePath}.corrompido-{suffix}";
        File.Move(databasePath, backup, overwrite: true);

        // Arquivos do modo WAL pertencem ao banco antigo e não podem ser aplicados ao novo.
        foreach (var companion in new[] { "-wal", "-shm" })
        {
            var path = databasePath + companion;
            if (File.Exists(path))
            {
                File.Move(path, backup + companion, overwrite: true);
            }
        }

        return backup;
    }
}
