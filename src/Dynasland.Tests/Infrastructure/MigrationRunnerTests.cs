using Dynasland.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Infrastructure;

public class MigrationRunnerTests
{
    [Fact]
    public async Task Creates_database_and_schema_from_nothing()
    {
        using var db = new TemporaryDatabase();

        var applied = await db.CreateRunner().MigrateAsync();

        Assert.Equal(3, applied);
        await using var connection = await db.Factory.OpenAsync();
        Assert.True(await TableExistsAsync(connection, "settings"));
        Assert.True(await TableExistsAsync(connection, "modules"));
        Assert.True(await TableExistsAsync(connection, "schema_migrations"));
    }

    [Fact]
    public async Task Running_twice_applies_nothing_the_second_time()
    {
        using var db = new TemporaryDatabase();

        await db.CreateRunner().MigrateAsync();
        var appliedAgain = await db.CreateRunner().MigrateAsync();

        Assert.Equal(0, appliedAgain);
    }

    [Fact]
    public async Task Applies_only_new_migrations_on_upgrade()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();

        var upgraded = new MigrationRunner(
            db.Factory,
            [new Dynasland.Infrastructure.Database.Migrations.M001_InitialSchema(), new AddNotesTable()],
            NullLogger<MigrationRunner>.Instance);
        var applied = await upgraded.MigrateAsync();

        Assert.Equal(1, applied);
        await using var connection = await db.Factory.OpenAsync();
        Assert.True(await TableExistsAsync(connection, "notes"));
    }

    [Fact]
    public async Task Failed_migration_is_rolled_back()
    {
        using var db = new TemporaryDatabase();
        var runner = new MigrationRunner(db.Factory, [new BrokenMigration()], NullLogger<MigrationRunner>.Instance);

        await Assert.ThrowsAsync<SqliteException>(() => runner.MigrateAsync());

        await using var connection = await db.Factory.OpenAsync();
        Assert.False(await TableExistsAsync(connection, "half_done"));
    }

    [Fact]
    public async Task Duplicate_versions_are_rejected()
    {
        using var db = new TemporaryDatabase();
        var runner = new MigrationRunner(db.Factory, [new AddNotesTable(), new AddNotesTable()], NullLogger<MigrationRunner>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.MigrateAsync());
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
    }

    private sealed class AddNotesTable : SqlMigration
    {
        public override int Version => 99;

        public override string Name => "Teste: notas";

        protected override string Sql => "CREATE TABLE notes (id INTEGER PRIMARY KEY);";
    }

    private sealed class BrokenMigration : SqlMigration
    {
        public override int Version => 1;

        public override string Name => "Teste: quebrada";

        protected override string Sql => "CREATE TABLE half_done (id INTEGER); SELECT * FROM tabela_inexistente;";
    }
}
