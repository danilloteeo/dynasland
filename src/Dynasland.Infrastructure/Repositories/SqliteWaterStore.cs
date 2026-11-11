using System.Globalization;
using Dynasland.Core.Interfaces;
using Dynasland.Infrastructure.Database;

namespace Dynasland.Infrastructure.Repositories;

public sealed class SqliteWaterStore(SqliteConnectionFactory factory) : IWaterStore
{
    public event EventHandler? Changed;

    public async Task<int> GetAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var rows = await Db.QueryAsync(factory, "SELECT glasses FROM water_log WHERE day = $day;",
            r => r.GetInt32(0), cancellationToken, ("$day", Key(day)));
        return rows.Count == 0 ? 0 : rows[0];
    }

    public async Task<int> AddAsync(DateOnly day, int delta, CancellationToken cancellationToken = default)
    {
        await Db.ExecuteAsync(factory, """
            INSERT INTO water_log (day, glasses) VALUES ($day, MAX(0, $delta))
            ON CONFLICT (day) DO UPDATE SET glasses = MAX(0, glasses + $delta);
            """,
            cancellationToken, ("$day", Key(day)), ("$delta", delta));
        Changed?.Invoke(this, EventArgs.Empty);
        return await GetAsync(day, cancellationToken);
    }

    private static string Key(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
