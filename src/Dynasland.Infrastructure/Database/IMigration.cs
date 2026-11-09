using Microsoft.Data.Sqlite;

namespace Dynasland.Infrastructure.Database;

/// <summary>Alteração de esquema aplicada uma única vez, em ordem crescente de versão.</summary>
public interface IMigration
{
    int Version { get; }

    string Name { get; }

    Task UpAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken);
}
