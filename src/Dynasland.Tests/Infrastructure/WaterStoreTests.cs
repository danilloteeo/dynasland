using Dynasland.Infrastructure.Repositories;

namespace Dynasland.Tests.Infrastructure;

public class WaterStoreTests
{
    [Fact]
    public async Task Counts_glasses_per_day_and_never_goes_below_zero()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var store = new SqliteWaterStore(db.Factory);
        var monday = new DateOnly(2026, 11, 9);
        var tuesday = new DateOnly(2026, 11, 10);
        var changes = 0;
        store.Changed += (_, _) => changes++;

        Assert.Equal(0, await store.GetAsync(monday));
        Assert.Equal(1, await store.AddAsync(monday, 1));
        Assert.Equal(2, await store.AddAsync(monday, 1));
        Assert.Equal(1, await store.AddAsync(monday, -1));
        Assert.Equal(0, await store.AddAsync(monday, -5));
        Assert.Equal(0, await store.AddAsync(tuesday, -1));

        Assert.Equal(0, await store.GetAsync(tuesday));
        Assert.Equal(1, await store.AddAsync(tuesday, 1));
        Assert.Equal(0, await store.GetAsync(monday));
        Assert.Equal(6, changes);
    }
}
