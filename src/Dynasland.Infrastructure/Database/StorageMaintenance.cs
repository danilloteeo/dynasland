using System.Text.Json;
using Dynasland.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Dynasland.Infrastructure.Database;

/// <summary>Arquivo JSON com as configurações (pares chave/valor), usado na exportação e na cópia automática.</summary>
public static class SettingsBackupFile
{
    private const string Format = "dynasland-settings";

    public static string AutomaticPath(DatabaseOptions options) =>
        Path.Combine(Path.GetDirectoryName(options.DatabasePath) ?? ".", "settings-backup.json");

    public static async Task WriteAsync(string path, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new BackupDocument(Format, DateTimeOffset.UtcNow, new SortedDictionary<string, string>(values.ToDictionary(), StringComparer.Ordinal));
        var temporary = path + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        }

        // Troca atômica: um backup interrompido no meio não substitui o anterior.
        File.Move(temporary, path, overwrite: true);
    }

    /// <exception cref="InvalidDataException">O arquivo não é uma cópia de configurações do Dynasland.</exception>
    public static async Task<IReadOnlyDictionary<string, string>> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        BackupDocument? document;
        try
        {
            document = await JsonSerializer.DeserializeAsync<BackupDocument>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("O arquivo não é uma cópia de configurações do Dynasland.", ex);
        }

        if (document is not { Format: Format, Settings: not null })
        {
            throw new InvalidDataException("O arquivo não é uma cópia de configurações do Dynasland.");
        }

        return document.Settings;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private sealed record BackupDocument(string Format, DateTimeOffset ExportedAt, SortedDictionary<string, string>? Settings);
}

public sealed class StorageMaintenance(
    DatabaseOptions options,
    SqliteConnectionFactory connectionFactory,
    ISettingsStore settingsStore,
    IIslandSettingsService settings,
    IClipboardHistoryStore clipboard,
    INotificationHistoryStore notifications,
    TimeProvider time,
    ILogger<StorageMaintenance> logger) : IStorageMaintenance
{
    public string DatabasePath => options.DatabasePath;

    public long GetDatabaseSizeBytes()
    {
        // No modo WAL, dados recentes ficam no arquivo -wal até o checkpoint.
        return new[] { options.DatabasePath, options.DatabasePath + "-wal" }
            .Where(File.Exists)
            .Sum(path => new FileInfo(path).Length);
    }

    public async Task ApplyRetentionAsync(CancellationToken cancellationToken = default)
    {
        var current = settings.Current;
        await clipboard.TrimAsync(current.ClipboardMaxItems, cancellationToken);
        await notifications.DeleteOlderThanAsync(time.GetUtcNow().AddDays(-current.NotificationHistoryDays), cancellationToken);
        if (!current.ClipboardHistoryEnabled)
        {
            await clipboard.ClearAsync(keepFavorites: true, cancellationToken);
        }

        if (!current.NotificationHistoryEnabled)
        {
            await notifications.ClearAsync(cancellationToken);
        }
    }

    public async Task CompactAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); VACUUM;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Banco compactado: {Size} bytes.", GetDatabaseSizeBytes());
    }

    public async Task ExportSettingsAsync(string path, CancellationToken cancellationToken = default) =>
        await SettingsBackupFile.WriteAsync(path, await settingsStore.GetAllAsync(cancellationToken), cancellationToken);

    public async Task<int> ImportSettingsAsync(string path, CancellationToken cancellationToken = default)
    {
        var values = await SettingsBackupFile.ReadAsync(path, cancellationToken);
        await settingsStore.ClearAsync(cancellationToken);
        await settingsStore.SetManyAsync(values, cancellationToken);
        logger.LogInformation("{Count} configurações importadas de {Path}.", values.Count, path);
        return values.Count;
    }

    public async Task WriteAutomaticBackupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await ExportSettingsAsync(SettingsBackupFile.AutomaticPath(options), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            logger.LogWarning(ex, "Não foi possível gravar a cópia automática das configurações.");
        }
    }
}
