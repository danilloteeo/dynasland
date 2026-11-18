using DynaIsland.Infrastructure.Database;

namespace DynaIsland.Tests.Infrastructure;

public sealed class LegacyDataMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dynaisland-legacy-" + Guid.NewGuid().ToString("N"));

    private string Legacy => Path.Combine(_root, "Dynasland");

    private string Current => Path.Combine(_root, "DynaIsland");

    [Fact]
    public void Copies_the_legacy_folder_and_renames_the_database()
    {
        Write(Legacy, "dynasland.db", "banco");
        Write(Legacy, "dynasland.db-wal", "wal");
        Write(Legacy, Path.Combine("logs", "dynasland.log"), "log");
        Write(Legacy, Path.Combine("updates", "pending.txt"), "1.7.1");

        var result = LegacyDataMigration.Run(Current, Legacy);

        Assert.Equal(LegacyMigrationResult.Migrated, result);
        Assert.Equal("banco", File.ReadAllText(Path.Combine(Current, "dynaisland.db")));
        Assert.Equal("wal", File.ReadAllText(Path.Combine(Current, "dynaisland.db-wal")));
        Assert.False(File.Exists(Path.Combine(Current, "dynasland.db")));
        Assert.Equal("1.7.1", File.ReadAllText(Path.Combine(Current, "updates", "pending.txt")));
    }

    [Fact]
    public void Leaves_the_legacy_folder_untouched()
    {
        Write(Legacy, "dynasland.db", "banco");

        LegacyDataMigration.Run(Current, Legacy);

        Assert.Equal("banco", File.ReadAllText(Path.Combine(Legacy, "dynasland.db")));
    }

    [Fact]
    public void Does_not_copy_the_webview_cache()
    {
        Write(Legacy, "dynasland.db", "banco");
        Write(Legacy, Path.Combine("webview2", "cache.bin"), "x");

        LegacyDataMigration.Run(Current, Legacy);

        Assert.False(Directory.Exists(Path.Combine(Current, "webview2")));
    }

    [Fact]
    public void Does_nothing_without_a_legacy_folder()
    {
        Assert.Equal(LegacyMigrationResult.NotNeeded, LegacyDataMigration.Run(Current, Legacy));
        Assert.False(Directory.Exists(Current));
    }

    [Fact]
    public void Does_not_overwrite_an_existing_database()
    {
        Write(Legacy, "dynasland.db", "antigo");
        Write(Current, "dynaisland.db", "novo");

        var result = LegacyDataMigration.Run(Current, Legacy);

        Assert.Equal(LegacyMigrationResult.NotNeeded, result);
        Assert.Equal("novo", File.ReadAllText(Path.Combine(Current, "dynaisland.db")));
    }

    [Fact]
    public void Migrates_only_once()
    {
        Write(Legacy, "dynasland.db", "banco");

        LegacyDataMigration.Run(Current, Legacy);
        File.WriteAllText(Path.Combine(Current, "dynaisland.db"), "editado");

        Assert.Equal(LegacyMigrationResult.NotNeeded, LegacyDataMigration.Run(Current, Legacy));
        Assert.Equal("editado", File.ReadAllText(Path.Combine(Current, "dynaisland.db")));
    }

    [Fact]
    public void Reports_failure_and_leaves_no_partial_folder()
    {
        Write(Legacy, "dynasland.db", "banco");
        // Um arquivo no lugar da pasta de destino impede a criação.
        Directory.CreateDirectory(_root);
        File.WriteAllText(Current, "bloqueio");

        var result = LegacyDataMigration.Run(Current, Legacy);

        Assert.Equal(LegacyMigrationResult.Failed, result);
        Assert.False(Directory.Exists(Current + ".migrando"));
    }

    private static void Write(string folder, string relative, string content)
    {
        var path = Path.Combine(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
