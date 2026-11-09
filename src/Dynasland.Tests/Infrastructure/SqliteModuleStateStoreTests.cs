using Dynasland.Infrastructure.Repositories;

namespace Dynasland.Tests.Infrastructure;

public class SqliteModuleStateStoreTests
{
    [Fact]
    public async Task States_round_trip_and_upsert()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var store = new SqliteModuleStateStore(db.Factory);

        Assert.Empty(await store.GetAllAsync());

        await store.SetEnabledAsync("clock", false);
        await store.SetEnabledAsync("media", true);
        await store.SetEnabledAsync("clock", true);

        var states = await store.GetAllAsync();
        Assert.True(states["clock"]);
        Assert.True(states["media"]);
        Assert.Equal(2, states.Count);
    }
}
