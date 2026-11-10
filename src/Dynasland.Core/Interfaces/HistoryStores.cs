using Dynasland.Core.Models;

namespace Dynasland.Core.Interfaces;

// Repositórios dos históricos e da agenda (SQLite em Infrastructure).

public interface IClipboardHistoryStore
{
    /// <summary>Guarda o item; se o mesmo conteúdo (hash) já existe, só atualiza a data de uso.</summary>
    Task<ClipboardRecord> AddOrTouchAsync(ClipboardEntryKind kind, string? text, byte[]? image, byte[]? thumbnail, string hash, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <param name="search">Trecho do texto (sem diferenciar maiúsculas); <c>null</c> = todos.</param>
    Task<IReadOnlyList<ClipboardRecord>> ListAsync(string? search = null, bool favoritesOnly = false, int limit = 200, CancellationToken cancellationToken = default);

    Task<ClipboardRecord?> GetAsync(long id, CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(long id, bool favorite, CancellationToken cancellationToken = default);

    Task TouchAsync(long id, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task ClearAsync(bool keepFavorites, CancellationToken cancellationToken = default);

    /// <summary>Mantém só os <paramref name="maxItems"/> mais recentes (favoritos nunca são apagados).</summary>
    Task TrimAsync(int maxItems, CancellationToken cancellationToken = default);
}

public interface INotificationHistoryStore
{
    Task AddAsync(NotificationRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationRecord>> ListAsync(int limit = 300, string? appId = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface IFocusSessionStore
{
    Task AddAsync(FocusSession session, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FocusSession>> ListSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface ICalendarStore
{
    /// <summary>Compromissos que se sobrepõem ao intervalo, por início.</summary>
    Task<IReadOnlyList<CalendarEvent>> ListAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <returns>O compromisso com o Id gerado (inclusão) ou o mesmo (alteração).</returns>
    Task<CalendarEvent> SaveAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Lembretes ainda não disparados, por horário do lembrete.</summary>
    Task<IReadOnlyList<CalendarEvent>> PendingRemindersAsync(CancellationToken cancellationToken = default);

    Task MarkRemindedAsync(long id, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Disparado (em qualquer thread) quando a agenda muda.</summary>
    event EventHandler? Changed;
}

public interface ICaptureHistoryStore
{
    Task<CaptureRecord> AddAsync(CaptureRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CaptureRecord>> ListAsync(int limit = 100, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public interface IWeatherCache
{
    Task<(string Payload, DateTimeOffset FetchedAt)?> GetAsync(string locationKey, CancellationToken cancellationToken = default);

    Task SetAsync(string locationKey, string payload, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default);
}

/// <summary>Tamanho do banco, retenção dos históricos, compactação e cópia de segurança das configurações.</summary>
public interface IStorageMaintenance
{
    string DatabasePath { get; }

    long GetDatabaseSizeBytes();

    /// <summary>Aplica os limites de retenção configurados.</summary>
    Task ApplyRetentionAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera o espaço de dados apagados (VACUUM).</summary>
    Task CompactAsync(CancellationToken cancellationToken = default);

    /// <summary>Exporta as configurações (sem históricos) para um arquivo JSON.</summary>
    Task ExportSettingsAsync(string path, CancellationToken cancellationToken = default);

    /// <returns>Quantidade de configurações importadas.</returns>
    Task<int> ImportSettingsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Cópia automática das configurações ao lado do banco (usada se o banco corromper).</summary>
    Task WriteAutomaticBackupAsync(CancellationToken cancellationToken = default);
}
