namespace Dynasland.Infrastructure.Database.Migrations;

/// <summary>
/// Históricos dos módulos (área de transferência, notificações, foco, capturas), agenda e cache do clima.
/// Datas em UTC no formato ISO 8601 ("O"), que ordena corretamente como texto.
/// </summary>
public sealed class M002_Histories : SqlMigration
{
    public override int Version => 2;

    public override string Name => "Históricos, agenda e cache do clima";

    protected override string Sql => """
        CREATE TABLE clipboard_items (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            kind         INTEGER NOT NULL,
            text         TEXT,
            image        BLOB,
            thumbnail    BLOB,
            hash         TEXT NOT NULL UNIQUE,
            is_favorite  INTEGER NOT NULL DEFAULT 0,
            created_at   TEXT NOT NULL,
            last_used_at TEXT NOT NULL
        );
        CREATE INDEX ix_clipboard_items_last_used ON clipboard_items (last_used_at DESC);

        CREATE TABLE notification_history (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            app_id      TEXT NOT NULL,
            app_name    TEXT NOT NULL,
            title       TEXT NOT NULL,
            body        TEXT NOT NULL,
            received_at TEXT NOT NULL,
            was_shown   INTEGER NOT NULL
        );
        CREATE INDEX ix_notification_history_received ON notification_history (received_at DESC);
        CREATE INDEX ix_notification_history_app ON notification_history (app_id);

        CREATE TABLE focus_sessions (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            kind            TEXT NOT NULL,
            started_at      TEXT NOT NULL,
            ended_at        TEXT NOT NULL,
            planned_seconds INTEGER NOT NULL,
            actual_seconds  INTEGER NOT NULL,
            completed       INTEGER NOT NULL
        );
        CREATE INDEX ix_focus_sessions_started ON focus_sessions (started_at DESC);

        CREATE TABLE calendar_events (
            id               INTEGER PRIMARY KEY AUTOINCREMENT,
            title            TEXT NOT NULL,
            notes            TEXT NOT NULL DEFAULT '',
            starts_at        TEXT NOT NULL,
            ends_at          TEXT NOT NULL,
            all_day          INTEGER NOT NULL,
            reminder_minutes INTEGER,
            reminded_at      TEXT
        );
        CREATE INDEX ix_calendar_events_starts ON calendar_events (starts_at);

        CREATE TABLE capture_history (
            id               INTEGER PRIMARY KEY AUTOINCREMENT,
            kind             TEXT NOT NULL,
            path             TEXT NOT NULL,
            created_at       TEXT NOT NULL,
            size_bytes       INTEGER NOT NULL,
            duration_seconds REAL
        );
        CREATE INDEX ix_capture_history_created ON capture_history (created_at DESC);

        CREATE TABLE weather_cache (
            location_key TEXT PRIMARY KEY NOT NULL,
            payload      TEXT NOT NULL,
            fetched_at   TEXT NOT NULL
        );
        """;
}
