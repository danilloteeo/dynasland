using Microsoft.Data.Sqlite;

namespace Dynasland.Infrastructure.Database;

/// <summary>Migração composta apenas por um script SQL.</summary>
public abstract class SqlMigration : IMigration
{
    public abstract int Version { get; }

    public abstract string Name { get; }

    protected abstract string Sql { get; }

    public async Task UpAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
