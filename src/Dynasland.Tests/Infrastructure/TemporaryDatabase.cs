using Dynasland.Infrastructure.Database;
using Dynasland.Infrastructure.Database.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Infrastructure;

/// <summary>Banco SQLite em arquivo temporário, apagado ao final do teste.</summary>
internal sealed class TemporaryDatabase : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dynasland-tests", Guid.NewGuid().ToString("N"));

    public TemporaryDatabase()
    {
        Options = new DatabaseOptions(Path.Combine(_directory, "sub", "test.db"));
        Factory = new SqliteConnectionFactory(Options);
    }

    public DatabaseOptions Options { get; }

    public SqliteConnectionFactory Factory { get; }

    public MigrationRunner CreateRunner() =>
        new(Factory, [new M001_InitialSchema()], NullLogger<MigrationRunner>.Instance);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
