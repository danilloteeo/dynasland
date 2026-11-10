using System.Globalization;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Infrastructure.Database;
using Microsoft.Data.Sqlite;

namespace Dynasland.Infrastructure.Repositories;

/// <summary>Conversões comuns: datas guardadas em UTC, formato ISO 8601 ("O"), ordenáveis como texto.</summary>
internal static class Db
{
    public static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset ReadTime(SqliteDataReader reader, int ordinal) =>
        DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    public static DateTimeOffset? ReadTimeOrNull(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ReadTime(reader, ordinal);

    public static string? ReadStringOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static byte[]? ReadBlobOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : (byte[])reader.GetValue(ordinal);

    public static object Value(object? value) => value ?? DBNull.Value;

    public static async Task<int> ExecuteAsync(SqliteConnectionFactory factory, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var connection = await factory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, Value(value));
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<List<T>> QueryAsync<T>(SqliteConnectionFactory factory, string sql, Func<SqliteDataReader, T> map, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var connection = await factory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, Value(value));
        }

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(map(reader));
        }

        return results;
    }
}

public sealed class SqliteClipboardHistoryStore(SqliteConnectionFactory factory) : IClipboardHistoryStore
{
    private const string Columns = "id, kind, text, image, thumbnail, hash, is_favorite, created_at, last_used_at";

    public async Task<ClipboardRecord> AddOrTouchAsync(ClipboardEntryKind kind, string? text, byte[]? image, byte[]? thumbnail, string hash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await Db.ExecuteAsync(factory, """
            INSERT INTO clipboard_items (kind, text, image, thumbnail, hash, is_favorite, created_at, last_used_at)
            VALUES ($kind, $text, $image, $thumbnail, $hash, 0, $now, $now)
            ON CONFLICT (hash) DO UPDATE SET last_used_at = excluded.last_used_at;
            """,
            cancellationToken,
            ("$kind", (int)kind), ("$text", text), ("$image", image), ("$thumbnail", thumbnail), ("$hash", hash), ("$now", Db.Time(now)));

        var items = await Db.QueryAsync(factory, $"SELECT {Columns} FROM clipboard_items WHERE hash = $hash;", Map, cancellationToken, ("$hash", hash));
        return items[0];
    }

    public async Task<IReadOnlyList<ClipboardRecord>> ListAsync(string? search = null, bool favoritesOnly = false, int limit = 200, CancellationToken cancellationToken = default)
    {
        // LIKE do SQLite já ignora maiúsculas/minúsculas para ASCII; a busca acentuada é refinada em memória.
        var items = await Db.QueryAsync(factory, $"""
            SELECT {Columns} FROM clipboard_items
            WHERE ($favorites = 0 OR is_favorite = 1)
            ORDER BY is_favorite DESC, last_used_at DESC
            LIMIT $limit;
            """,
            Map, cancellationToken, ("$favorites", favoritesOnly ? 1 : 0), ("$limit", string.IsNullOrWhiteSpace(search) ? limit : 5000));

        if (string.IsNullOrWhiteSpace(search))
        {
            return items;
        }

        var term = search.Trim();
        return [.. items.Where(i => i.Text?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true).Take(limit)];
    }

    public async Task<ClipboardRecord?> GetAsync(long id, CancellationToken cancellationToken = default) =>
        (await Db.QueryAsync(factory, $"SELECT {Columns} FROM clipboard_items WHERE id = $id;", Map, cancellationToken, ("$id", id))).FirstOrDefault();

    public Task SetFavoriteAsync(long id, bool favorite, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, "UPDATE clipboard_items SET is_favorite = $favorite WHERE id = $id;", cancellationToken, ("$favorite", favorite ? 1 : 0), ("$id", id));

    public Task TouchAsync(long id, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, "UPDATE clipboard_items SET last_used_at = $now WHERE id = $id;", cancellationToken, ("$now", Db.Time(now)), ("$id", id));

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, "DELETE FROM clipboard_items WHERE id = $id;", cancellationToken, ("$id", id));

    public Task ClearAsync(bool keepFavorites, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, keepFavorites ? "DELETE FROM clipboard_items WHERE is_favorite = 0;" : "DELETE FROM clipboard_items;", cancellationToken);

    public Task TrimAsync(int maxItems, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, """
            DELETE FROM clipboard_items
            WHERE is_favorite = 0 AND id NOT IN (
                SELECT id FROM clipboard_items WHERE is_favorite = 0 ORDER BY last_used_at DESC LIMIT $max);
            """,
            cancellationToken, ("$max", Math.Max(0, maxItems)));

    private static ClipboardRecord Map(SqliteDataReader r) => new(
        r.GetInt64(0),
        (ClipboardEntryKind)r.GetInt32(1),
        Db.ReadStringOrNull(r, 2),
        Db.ReadBlobOrNull(r, 3),
        Db.ReadBlobOrNull(r, 4),
        r.GetString(5),
        r.GetInt32(6) != 0,
        Db.ReadTime(r, 7),
        Db.ReadTime(r, 8));
}

public sealed class SqliteNotificationHistoryStore(SqliteConnectionFactory factory) : INotificationHistoryStore
{
    public Task AddAsync(NotificationRecord record, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, """
            INSERT INTO notification_history (app_id, app_name, title, body, received_at, was_shown)
            VALUES ($appId, $appName, $title, $body, $at, $shown);
            """,
            cancellationToken,
            ("$appId", record.AppId), ("$appName", record.AppName), ("$title", record.Title), ("$body", record.Body),
            ("$at", Db.Time(record.ReceivedAt)), ("$shown", record.WasShown ? 1 : 0));

    public async Task<IReadOnlyList<NotificationRecord>> ListAsync(int limit = 300, string? appId = null, CancellationToken cancellationToken = default) =>
        await Db.QueryAsync(factory, """
            SELECT id, app_id, app_name, title, body, received_at, was_shown FROM notification_history
            WHERE ($app IS NULL OR app_id = $app)
            ORDER BY received_at DESC LIMIT $limit;
            """,
            r => new NotificationRecord(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), Db.ReadTime(r, 5), r.GetInt32(6) != 0),
            cancellationToken, ("$app", appId), ("$limit", limit));

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, "DELETE FROM notification_history WHERE id = $id;", cancellationToken, ("$id", id));

    public Task DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, "DELETE FROM notification_history WHERE received_at < $cutoff;", cancellationToken, ("$cutoff", Db.Time(cutoff)));

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, "DELETE FROM notification_history;", cancellationToken);
}

public sealed class SqliteFocusSessionStore(SqliteConnectionFactory factory) : IFocusSessionStore
{
    public Task AddAsync(FocusSession session, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, """
            INSERT INTO focus_sessions (kind, started_at, ended_at, planned_seconds, actual_seconds, completed)
            VALUES ($kind, $start, $end, $planned, $actual, $completed);
            """,
            cancellationToken,
            ("$kind", session.Kind.ToString()), ("$start", Db.Time(session.StartedAt)), ("$end", Db.Time(session.EndedAt)),
            ("$planned", (long)session.Planned.TotalSeconds), ("$actual", (long)session.Actual.TotalSeconds), ("$completed", session.Completed ? 1 : 0));

    public async Task<IReadOnlyList<FocusSession>> ListSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default) =>
        await Db.QueryAsync(factory, """
            SELECT id, kind, started_at, ended_at, planned_seconds, actual_seconds, completed FROM focus_sessions
            WHERE started_at >= $since ORDER BY started_at DESC;
            """,
            r => new FocusSession(
                r.GetInt64(0),
                Enum.TryParse<FocusSessionKind>(r.GetString(1), out var kind) ? kind : FocusSessionKind.Focus,
                Db.ReadTime(r, 2),
                Db.ReadTime(r, 3),
                TimeSpan.FromSeconds(r.GetInt64(4)),
                TimeSpan.FromSeconds(r.GetInt64(5)),
                r.GetInt32(6) != 0),
            cancellationToken, ("$since", Db.Time(since)));

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, "DELETE FROM focus_sessions;", cancellationToken);
}

public sealed class SqliteCalendarStore(SqliteConnectionFactory factory) : ICalendarStore
{
    private const string Columns = "id, title, notes, starts_at, ends_at, all_day, reminder_minutes, reminded_at";

    public event EventHandler? Changed;

    public async Task<IReadOnlyList<CalendarEvent>> ListAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        await Db.QueryAsync(factory, $"""
            SELECT {Columns} FROM calendar_events
            WHERE starts_at < $to AND ends_at > $from
            ORDER BY starts_at;
            """,
            Map, cancellationToken, ("$from", Db.Time(from)), ("$to", Db.Time(to)));

    public async Task<CalendarEvent> SaveAsync(CalendarEvent e, CancellationToken cancellationToken = default)
    {
        var saved = e;
        if (e.Id == 0)
        {
            await using var connection = await factory.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO calendar_events (title, notes, starts_at, ends_at, all_day, reminder_minutes, reminded_at)
                VALUES ($title, $notes, $start, $end, $allDay, $reminder, NULL);
                SELECT last_insert_rowid();
                """;
            AddParameters(command, e);
            var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            saved = e with { Id = id, RemindedAt = null };
        }
        else
        {
            // Mudou o horário: o lembrete volta a valer.
            await Db.ExecuteAsync(factory, """
                UPDATE calendar_events SET title = $title, notes = $notes, starts_at = $start, ends_at = $end,
                    all_day = $allDay, reminder_minutes = $reminder, reminded_at = NULL
                WHERE id = $id;
                """,
                cancellationToken,
                ("$id", e.Id), ("$title", e.Title), ("$notes", e.Notes), ("$start", Db.Time(e.StartsAt)), ("$end", Db.Time(e.EndsAt)),
                ("$allDay", e.AllDay ? 1 : 0), ("$reminder", e.ReminderMinutes));
            saved = e with { RemindedAt = null };
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return saved;
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await Db.ExecuteAsync(factory, "DELETE FROM calendar_events WHERE id = $id;", cancellationToken, ("$id", id));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyList<CalendarEvent>> PendingRemindersAsync(CancellationToken cancellationToken = default)
    {
        var items = await Db.QueryAsync(factory, $"""
            SELECT {Columns} FROM calendar_events
            WHERE reminder_minutes IS NOT NULL AND reminded_at IS NULL
            ORDER BY starts_at;
            """, Map, cancellationToken);
        return [.. items.OrderBy(e => e.ReminderAt)];
    }

    public Task MarkRemindedAsync(long id, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, "UPDATE calendar_events SET reminded_at = $at WHERE id = $id;", cancellationToken, ("$at", Db.Time(at)), ("$id", id));

    private static void AddParameters(SqliteCommand command, CalendarEvent e)
    {
        command.Parameters.AddWithValue("$title", e.Title);
        command.Parameters.AddWithValue("$notes", e.Notes);
        command.Parameters.AddWithValue("$start", Db.Time(e.StartsAt));
        command.Parameters.AddWithValue("$end", Db.Time(e.EndsAt));
        command.Parameters.AddWithValue("$allDay", e.AllDay ? 1 : 0);
        command.Parameters.AddWithValue("$reminder", Db.Value(e.ReminderMinutes));
    }

    private static CalendarEvent Map(SqliteDataReader r) => new(
        r.GetInt64(0),
        r.GetString(1),
        r.GetString(2),
        Db.ReadTime(r, 3),
        Db.ReadTime(r, 4),
        r.GetInt32(5) != 0,
        r.IsDBNull(6) ? null : r.GetInt32(6),
        Db.ReadTimeOrNull(r, 7));
}

public sealed class SqliteCaptureHistoryStore(SqliteConnectionFactory factory) : ICaptureHistoryStore
{
    public async Task<CaptureRecord> AddAsync(CaptureRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = await factory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO capture_history (kind, path, created_at, size_bytes, duration_seconds)
            VALUES ($kind, $path, $at, $size, $duration);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$kind", record.Kind.ToString());
        command.Parameters.AddWithValue("$path", record.Path);
        command.Parameters.AddWithValue("$at", Db.Time(record.CreatedAt));
        command.Parameters.AddWithValue("$size", record.SizeBytes);
        command.Parameters.AddWithValue("$duration", Db.Value(record.DurationSeconds));
        var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        return record with { Id = id };
    }

    public async Task<IReadOnlyList<CaptureRecord>> ListAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        await Db.QueryAsync(factory, """
            SELECT id, kind, path, created_at, size_bytes, duration_seconds FROM capture_history
            ORDER BY created_at DESC LIMIT $limit;
            """,
            r => new CaptureRecord(
                r.GetInt64(0),
                Enum.TryParse<CaptureKind>(r.GetString(1), out var kind) ? kind : CaptureKind.Screenshot,
                r.GetString(2),
                Db.ReadTime(r, 3),
                r.GetInt64(4),
                r.IsDBNull(5) ? null : r.GetDouble(5)),
            cancellationToken, ("$limit", limit));

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, "DELETE FROM capture_history WHERE id = $id;", cancellationToken, ("$id", id));
}

public sealed class SqliteWeatherCache(SqliteConnectionFactory factory) : IWeatherCache
{
    public async Task<(string Payload, DateTimeOffset FetchedAt)?> GetAsync(string locationKey, CancellationToken cancellationToken = default)
    {
        var rows = await Db.QueryAsync(factory, "SELECT payload, fetched_at FROM weather_cache WHERE location_key = $key;",
            r => (r.GetString(0), Db.ReadTime(r, 1)), cancellationToken, ("$key", locationKey));
        return rows.Count == 0 ? null : rows[0];
    }

    public Task SetAsync(string locationKey, string payload, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync(factory, """
            INSERT INTO weather_cache (location_key, payload, fetched_at) VALUES ($key, $payload, $at)
            ON CONFLICT (location_key) DO UPDATE SET payload = excluded.payload, fetched_at = excluded.fetched_at;
            """,
            cancellationToken, ("$key", locationKey), ("$payload", payload), ("$at", Db.Time(fetchedAt)));
}
