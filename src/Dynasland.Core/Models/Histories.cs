namespace Dynasland.Core.Models;

/// <summary>Item do histórico próprio da área de transferência.</summary>
/// <param name="Hash">Identifica conteúdo repetido (copiar o mesmo texto de novo só atualiza a data).</param>
public sealed record ClipboardRecord(
    long Id,
    ClipboardEntryKind Kind,
    string? Text,
    byte[]? Image,
    byte[]? Thumbnail,
    string Hash,
    bool IsFavorite,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt);

/// <summary>Notificação recebida, guardada no histórico.</summary>
/// <param name="WasShown">Apareceu na ilha (falso se o aplicativo estava oculto pelos filtros).</param>
public sealed record NotificationRecord(long Id, string AppId, string AppName, string Title, string Body, DateTimeOffset ReceivedAt, bool WasShown);

public enum FocusSessionKind
{
    Focus,
    ShortBreak,
    LongBreak,
    Timer,
    Stopwatch,
}

public sealed record FocusSession(
    long Id,
    FocusSessionKind Kind,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    TimeSpan Planned,
    TimeSpan Actual,
    bool Completed);

/// <summary>Compromisso da agenda local.</summary>
/// <param name="ReminderMinutes">Minutos antes do início para lembrar; <c>null</c> = sem lembrete.</param>
public sealed record CalendarEvent(
    long Id,
    string Title,
    string Notes,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool AllDay,
    int? ReminderMinutes,
    DateTimeOffset? RemindedAt)
{
    public DateTimeOffset? ReminderAt => ReminderMinutes is { } minutes ? StartsAt.AddMinutes(-minutes) : null;
}

public enum CaptureKind
{
    Screenshot,
    Recording,
}

public sealed record CaptureRecord(long Id, CaptureKind Kind, string Path, DateTimeOffset CreatedAt, long SizeBytes, double? DurationSeconds);
