using System.Security.Cryptography;
using System.Text;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Services;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Clipboard;

/// <summary>
/// Histórico próprio da área de transferência. O <see cref="ClipboardModule"/> entrega cada cópia
/// (já filtrada pela marcação de privacidade do Windows); aqui são aplicadas as preferências de
/// privacidade, a detecção de dados sensíveis, a deduplicação e o limite de itens.
/// </summary>
public sealed class ClipboardHistory(
    IClipboardService clipboard,
    IClipboardHistoryStore store,
    IIslandSettingsService settings,
    TimeProvider time,
    ILogger<ClipboardHistory> logger) : IClipboardHistory
{
    private const int ThumbnailWidth = 160;

    // A cópia feita pelo próprio Dynasland também dispara o aviso do Windows.
    private static readonly TimeSpan OwnCopyWindow = TimeSpan.FromSeconds(1.5);

    private long _lastOwnCopyTicks;

    public event EventHandler? Changed;

    /// <summary>A mudança na área de transferência veio de uma cópia feita pelo Dynasland.</summary>
    public bool IsOwnCopy => time.GetUtcNow().UtcTicks - Interlocked.Read(ref _lastOwnCopyTicks) < OwnCopyWindow.Ticks;

    public void MarkOwnCopy() => Interlocked.Exchange(ref _lastOwnCopyTicks, time.GetUtcNow().UtcTicks);

    /// <returns><c>true</c> se o item foi guardado.</returns>
    public async Task<bool> CaptureAsync(ClipboardEntry entry, CancellationToken cancellationToken = default)
    {
        var current = settings.Current;
        if (!current.ClipboardHistoryEnabled)
        {
            return false;
        }

        string hash;
        byte[]? thumbnail = null;
        switch (entry.Kind)
        {
            case ClipboardEntryKind.Text when !string.IsNullOrWhiteSpace(entry.Text):
                if (SensitiveContentFilter.IsLikelySensitive(entry.Text))
                {
                    logger.LogDebug("Texto com aparência de dado sensível não foi guardado.");
                    return false;
                }

                hash = Hash("t:", Encoding.UTF8.GetBytes(entry.Text));
                break;

            case ClipboardEntryKind.Image when entry.Image is { Length: > 0 } image && current.ClipboardStoreImages:
                thumbnail = ImageDecoder.EncodeThumbnailPng(image, ThumbnailWidth);
                if (thumbnail is null)
                {
                    return false;
                }

                hash = Hash("i:", image);
                break;

            default:
                // Arquivos e formatos especiais não podem ser recolocados a partir do histórico.
                return false;
        }

        await store.AddOrTouchAsync(entry.Kind, entry.Kind == ClipboardEntryKind.Text ? entry.Text : null, entry.Image, thumbnail, hash, time.GetUtcNow(), cancellationToken);
        await store.TrimAsync(current.ClipboardMaxItems, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public Task<IReadOnlyList<ClipboardRecord>> ListAsync(string? search = null, bool favoritesOnly = false, int limit = 200, CancellationToken cancellationToken = default) =>
        store.ListAsync(search, favoritesOnly, limit, cancellationToken);

    public async Task<bool> CopyAsync(long id, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync(id, cancellationToken) is not { } record)
        {
            return false;
        }

        MarkOwnCopy();
        var copied = record.Kind == ClipboardEntryKind.Image && record.Image is { } image
            ? await clipboard.SetImageAsync(image)
            : record.Text is { } text && await clipboard.SetTextAsync(text);

        if (copied)
        {
            await store.TouchAsync(id, time.GetUtcNow(), cancellationToken);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return copied;
    }

    public async Task SetFavoriteAsync(long id, bool favorite, CancellationToken cancellationToken = default)
    {
        await store.SetFavoriteAsync(id, favorite, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await store.DeleteAsync(id, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearAsync(bool keepFavorites, CancellationToken cancellationToken = default)
    {
        await store.ClearAsync(keepFavorites, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Hash(string prefix, byte[] data)
    {
        using var sha = SHA256.Create();
        sha.TransformBlock(Encoding.ASCII.GetBytes(prefix), 0, prefix.Length, null, 0);
        sha.TransformFinalBlock(data, 0, data.Length);
        return Convert.ToHexString(sha.Hash!);
    }
}
