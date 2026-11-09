using System.Globalization;
using Dynasland.Core.Interfaces;
using Dynasland.Infrastructure.Database;

namespace Dynasland.Infrastructure.Repositories;

public sealed class SqliteModuleStateStore(SqliteConnectionFactory connectionFactory) : IModuleStateStore
{
    public async Task<IReadOnlyDictionary<string, bool>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, enabled FROM modules;";

        var states = new Dictionary<string, bool>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            states[reader.GetString(0)] = reader.GetInt64(1) != 0;
        }

        return states;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM modules;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(string moduleId, bool enabled, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO modules (id, enabled, updated_at) VALUES ($id, $enabled, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET enabled = excluded.enabled, updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$id", moduleId);
        command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
