using Dynasland.Core.Models;
using Dynasland.Infrastructure.Repositories;

namespace Dynasland.Tests.Infrastructure;

public class HistoryStoresTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Clipboard_deduplicates_searches_favorites_and_trims()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var store = new SqliteClipboardHistoryStore(db.Factory);

        var first = await store.AddOrTouchAsync(ClipboardEntryKind.Text, "Olá Mundo", null, null, "h1", T0);
        await store.AddOrTouchAsync(ClipboardEntryKind.Text, "segundo", null, null, "h2", T0.AddMinutes(1));
        await store.AddOrTouchAsync(ClipboardEntryKind.Text, "terceiro", null, null, "h3", T0.AddMinutes(2));
        var again = await store.AddOrTouchAsync(ClipboardEntryKind.Text, "Olá Mundo", null, null, "h1", T0.AddMinutes(3));

        Assert.Equal(first.Id, again.Id);
        Assert.Equal("Olá Mundo", (await store.ListAsync())[0].Text);
        Assert.Single(await store.ListAsync("OLÁ"));

        await store.SetFavoriteAsync(first.Id, true);
        await store.TrimAsync(1);

        var remaining = await store.ListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.True(remaining[0].IsFavorite);
        Assert.Equal("terceiro", remaining[1].Text);

        await store.ClearAsync(keepFavorites: true);
        Assert.Single(await store.ListAsync());
        Assert.Single(await store.ListAsync(favoritesOnly: true));
    }

    [Fact]
    public async Task Notifications_are_listed_newest_first_and_expire()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var store = new SqliteNotificationHistoryStore(db.Factory);

        await store.AddAsync(new NotificationRecord(0, "app", "App", "Velha", "x", T0.AddDays(-10), true));
        await store.AddAsync(new NotificationRecord(0, "app", "App", "Nova", "y", T0, false));

        Assert.Equal("Nova", (await store.ListAsync())[0].Title);
        await store.DeleteOlderThanAsync(T0.AddDays(-7));
        var list = await store.ListAsync();
        Assert.Single(list);
        Assert.False(list[0].WasShown);
    }

    [Fact]
    public async Task Calendar_lists_overlapping_events_and_tracks_reminders()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var store = new SqliteCalendarStore(db.Factory);
        var changes = 0;
        store.Changed += (_, _) => changes++;

        var meeting = await store.SaveAsync(new CalendarEvent(0, "Reunião", "", T0.AddHours(2), T0.AddHours(3), false, 15, null));
        await store.SaveAsync(new CalendarEvent(0, "Amanhã", "", T0.AddDays(1), T0.AddDays(1).AddHours(1), false, null, null));

        Assert.True(meeting.Id > 0);
        Assert.Equal(2, changes);
        Assert.Single(await store.ListAsync(T0, T0.AddHours(12)));
        Assert.Equal(T0.AddHours(2).AddMinutes(-15), (await store.PendingRemindersAsync()).Single().ReminderAt);

        await store.MarkRemindedAsync(meeting.Id, T0);
        Assert.Empty(await store.PendingRemindersAsync());

        // Alterar o horário reativa o lembrete.
        await store.SaveAsync(meeting with { StartsAt = T0.AddHours(5), EndsAt = T0.AddHours(6) });
        Assert.Single(await store.PendingRemindersAsync());

        await store.DeleteAsync(meeting.Id);
        Assert.Empty(await store.PendingRemindersAsync());
    }

    [Fact]
    public async Task Focus_sessions_and_captures_round_trip()
    {
        using var db = new TemporaryDatabase();
        await db.CreateRunner().MigrateAsync();
        var focus = new SqliteFocusSessionStore(db.Factory);
        var captures = new SqliteCaptureHistoryStore(db.Factory);
        var weather = new SqliteWeatherCache(db.Factory);

        await focus.AddAsync(new FocusSession(0, FocusSessionKind.Focus, T0, T0.AddMinutes(25), TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(25), true));
        var session = (await focus.ListSinceAsync(T0.AddDays(-1))).Single();
        Assert.Equal(TimeSpan.FromMinutes(25), session.Actual);
        Assert.True(session.Completed);

        var record = await captures.AddAsync(new CaptureRecord(0, CaptureKind.Recording, @"C:\x.mp4", T0, 1234, 12.5));
        Assert.Equal(12.5, (await captures.ListAsync()).Single().DurationSeconds);
        await captures.DeleteAsync(record.Id);
        Assert.Empty(await captures.ListAsync());

        await weather.SetAsync("k", "{}", T0);
        await weather.SetAsync("k", "{\"a\":1}", T0.AddHours(1));
        Assert.Equal(("{\"a\":1}", T0.AddHours(1)), await weather.GetAsync("k"));
    }
}
