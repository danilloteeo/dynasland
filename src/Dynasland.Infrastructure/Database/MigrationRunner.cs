using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Database;

/// <summary>
/// Cria o banco quando ausente e aplica as migrações pendentes, cada uma em sua própria transação.
/// As versões aplicadas ficam registradas em <c>schema_migrations</c>.
/// </summary>
public sealed class MigrationRunner(
    SqliteConnectionFactory connectionFactory,
    IEnumerable<IMigration> migrations,
    ILogger<MigrationRunner> logger)
{
    /// <returns>Quantidade de migrações aplicadas.</returns>
    public async Task<int> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var ordered = migrations.OrderBy(m => m.Version).ToList();
        var duplicated = ordered.GroupBy(m => m.Version).FirstOrDefault(g => g.Count() > 1);
        if (duplicated is not null)
        {
            throw new InvalidOperationException($"Versão de migração duplicada: {duplicated.Key}.");
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, "PRAGMA journal_mode = WAL;", cancellationToken);
        await ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version    INTEGER PRIMARY KEY NOT NULL,
                name       TEXT NOT NULL,
                applied_at TEXT NOT NULL
            );
            """, cancellationToken);

        var currentVersion = await GetCurrentVersionAsync(connection, cancellationToken);
        var applied = 0;

        foreach (var migration in ordered.Where(m => m.Version > currentVersion))
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await migration.UpAsync(connection, transaction, cancellationToken);

            await using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText = "INSERT INTO schema_migrations (version, name, applied_at) VALUES ($version, $name, $appliedAt);";
                record.Parameters.AddWithValue("$version", migration.Version);
                record.Parameters.AddWithValue("$name", migration.Name);
                record.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await record.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            applied++;
            logger.LogInformation("Migração {Version} ({Name}) aplicada.", migration.Version, migration.Name);
        }

        return applied;
    }

    private static async Task<long> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
