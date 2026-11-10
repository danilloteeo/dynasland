using Dynasland.Infrastructure.Database;
using Dynasland.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Infrastructure;

public class DatabaseRecoveryTests
{
    private static DatabaseRecovery CreateRecovery(TemporaryDatabase db) =>
        new(db.Options, db.CreateRunner(), NullLogger<DatabaseRecovery>.Instance);

    [Fact]
    public async Task Healthy_database_is_kept()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        await new SqliteSettingsStore(db.Factory).SetManyAsync(new Dictionary<string, string> { ["chave"] = "valor" });

        var backup = await CreateRecovery(db).MigrateOrRecoverAsync();

        Assert.Null(backup);
        Assert.Equal("valor", (await new SqliteSettingsStore(db.Factory).GetAllAsync())["chave"]);
    }

    [Fact]
    public async Task Garbage_file_is_moved_aside_and_database_recreated()
    {
        using var db = new TemporaryDatabase();
        Directory.CreateDirectory(Path.GetDirectoryName(db.Options.DatabasePath)!);
        await File.WriteAllBytesAsync(db.Options.DatabasePath, Enumerable.Repeat((byte)0x5A, 8192).ToArray());

        var backup = await CreateRecovery(db).MigrateOrRecoverAsync();

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup));
        await new SqliteSettingsStore(db.Factory).SetManyAsync(new Dictionary<string, string> { ["chave"] = "valor" });
        Assert.Equal("valor", (await new SqliteSettingsStore(db.Factory).GetAllAsync())["chave"]);
    }

    [Fact]
    public async Task Damaged_pages_are_detected_by_integrity_check()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var store = new SqliteSettingsStore(db.Factory);
        for (var i = 0; i < 300; i++)
        {
            await store.SetManyAsync(new Dictionary<string, string> { [$"chave{i}"] = new string('x', 200) });
        }

        await CheckpointAsync(db);
        SqliteConnection.ClearAllPools();

        // Mantém o cabeçalho (página 1) e estraga as páginas seguintes.
        await using (var file = new FileStream(db.Options.DatabasePath, FileMode.Open, FileAccess.Write))
        {
            file.Seek(4096 + 16, SeekOrigin.Begin);
            var noise = Enumerable.Repeat((byte)0xFF, (int)Math.Min(16384, file.Length - 4096 - 16)).ToArray();
            await file.WriteAsync(noise);
        }

        var backup = await CreateRecovery(db).MigrateOrRecoverAsync();

        Assert.NotNull(backup);
        Assert.Empty(await new SqliteSettingsStore(db.Factory).GetAllAsync());
    }

    private static async Task CheckpointAsync(TemporaryDatabase db)
    {
        await using var connection = await db.Factory.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await command.ExecuteNonQueryAsync();
    }
}
