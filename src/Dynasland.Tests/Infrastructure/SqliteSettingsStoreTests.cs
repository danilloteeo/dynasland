using Dynasland.Core.Models;
using Dynasland.Core.Services;
using Dynasland.Infrastructure.Repositories;

namespace Dynasland.Tests.Infrastructure;

public class SqliteSettingsStoreTests
{
    [Fact]
    public async Task Empty_database_returns_no_values()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();

        var values = await new SqliteSettingsStore(db.Factory).GetAllAsync();

        Assert.Empty(values);
    }

    [Fact]
    public async Task Values_round_trip_and_upsert()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var store = new SqliteSettingsStore(db.Factory);

        await store.SetManyAsync(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
        await store.SetManyAsync(new Dictionary<string, string> { ["b"] = "3" });

        var values = await store.GetAllAsync();
        Assert.Equal("1", values["a"]);
        Assert.Equal("3", values["b"]);
    }

    [Fact]
    public async Task Island_settings_survive_restart()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();

        using (var first = new IslandSettingsService(new SqliteSettingsStore(db.Factory)))
        {
            await first.UpdateAsync(s => s with { IsVisible = false, AnimationSpeed = 1.5, MonitorDeviceName = @"\\.\DISPLAY2" });
        }

        using var second = new IslandSettingsService(new SqliteSettingsStore(db.Factory));
        await second.LoadAsync();

        Assert.Equal(IslandSettings.Default with { IsVisible = false, AnimationSpeed = 1.5, MonitorDeviceName = @"\\.\DISPLAY2" }, second.Current);
    }
}
