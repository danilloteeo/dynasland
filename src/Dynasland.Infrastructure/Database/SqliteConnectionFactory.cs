using Microsoft.Data.Sqlite;

namespace Dynasland.Infrastructure.Database;

public sealed class SqliteConnectionFactory
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public SqliteConnectionFactory(DatabaseOptions options)
    {
        _databasePath = options.DatabasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
