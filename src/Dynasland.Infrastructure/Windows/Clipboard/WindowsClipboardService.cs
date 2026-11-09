using System.Collections.Concurrent;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Dynasland.Infrastructure.Windows.Clipboard;

/// <summary>
/// Área de transferência: detecção de cópias via Win32 e conteúdo/histórico via API do Windows
/// (o mesmo histórico do Win+V). Nada é armazenado pelo Dynasland.
/// </summary>
/// <remarks>Chamadas WinRT são feitas no pool (MTA), como nos demais serviços.</remarks>
public sealed class WindowsClipboardService(ILogger<WindowsClipboardService> logger) : IClipboardService, IDisposable
{
    private const int MaxTextLength = 2000;

    private readonly ConcurrentDictionary<string, ClipboardHistoryItem> _historyItems = new();
    private ClipboardListenerWindow? _listener;

    public event EventHandler? ContentChanged;

    public bool IsHistoryEnabled
    {
        get
        {
            try
            {
                return WinClipboard.IsHistoryEnabled();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener = ClipboardListenerWindow.Start(OnClipboardUpdate);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _listener?.Dispose();
        _listener = null;
        _historyItems.Clear();
    }

    public Task<ClipboardEntry?> GetCurrentAsync() => Task.Run(async () =>
    {
        try
        {
            return await ReadAsync(WinClipboard.GetContent(), string.Empty, DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            // Outro processo pode estar com a área de transferência aberta.
            logger.LogDebug(ex, "Conteúdo da área de transferência indisponível.");
            return null;
        }
    });

    public Task<IReadOnlyList<ClipboardEntry>> GetHistoryAsync(int maxItems) => Task.Run<IReadOnlyList<ClipboardEntry>>(async () =>
    {
        if (!IsHistoryEnabled)
        {
            return [];
        }

        try
        {
            var result = await WinClipboard.GetHistoryItemsAsync();
            if (result.Status != ClipboardHistoryItemsResultStatus.Success)
            {
                return [];
            }

            _historyItems.Clear();
            var entries = new List<ClipboardEntry>();
            foreach (var item in result.Items.Take(maxItems))
            {
                _historyItems[item.Id] = item;
                if (await ReadAsync(item.Content, item.Id, item.Timestamp) is { } entry)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Histórico da área de transferência indisponível.");
            return [];
        }
    });

    public Task<bool> CopyAgainAsync(string entryId) => Task.Run(() =>
    {
        if (!_historyItems.TryGetValue(entryId, out var item))
        {
            return false;
        }

        try
        {
            return WinClipboard.SetHistoryItemAsContent(item) == SetHistoryItemAsContentStatus.Success;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível copiar novamente o item do histórico.");
            return false;
        }
    });

    private void OnClipboardUpdate(IntPtr hwnd)
    {
        if (ClipboardPrivacy.IsPrivate(hwnd))
        {
            logger.LogDebug("Conteúdo privado copiado; ignorado.");
            return;
        }

        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private static async Task<ClipboardEntry?> ReadAsync(DataPackageView content, string id, DateTimeOffset timestamp)
    {
        if (content.Contains(StandardDataFormats.Text))
        {
            var text = await content.GetTextAsync();
            if (text.Length > MaxTextLength)
            {
                text = text[..MaxTextLength];
            }

            return new ClipboardEntry(id, ClipboardEntryKind.Text, text, null, timestamp);
        }

        if (content.Contains(StandardDataFormats.Bitmap))
        {
            var bitmap = await content.GetBitmapAsync();
            return new ClipboardEntry(id, ClipboardEntryKind.Image, null, await WinRtStreams.ReadAllBytesAsync(bitmap), timestamp);
        }

        if (content.Contains(StandardDataFormats.StorageItems))
        {
            var items = await content.GetStorageItemsAsync();
            var names = string.Join(", ", items.Take(3).Select(i => i.Name));
            var label = items.Count > 3 ? $"{names} e mais {items.Count - 3}" : names;
            return new ClipboardEntry(id, ClipboardEntryKind.Other, label, null, timestamp);
        }

        return content.AvailableFormats.Count == 0 ? null : new ClipboardEntry(id, ClipboardEntryKind.Other, null, null, timestamp);
    }
}
