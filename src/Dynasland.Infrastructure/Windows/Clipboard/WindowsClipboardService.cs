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
    // Textos maiores são cortados (o histórico guarda até este tamanho).
    private const int MaxTextLength = 100_000;

    private readonly ConcurrentDictionary<string, ClipboardHistoryItem> _historyItems = new();
    private readonly StaWorker _sta = new("Dynasland.ClipboardSta");
    private ClipboardListenerWindow? _listener;

    public event EventHandler? ContentChanged;

    public bool IsHistoryEnabled
    {
        get
        {
            try
            {
                return _sta.Run(WinClipboard.IsHistoryEnabled).GetAwaiter().GetResult();
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

    public Task<ClipboardEntry?> GetCurrentAsync() => RunOnOwnThread(() =>
    {
        try
        {
            return Read(WinClipboard.GetContent(), string.Empty, DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            // Outro processo pode estar com a área de transferência aberta.
            logger.LogWarning(ex, "Conteúdo da área de transferência indisponível.");
            return null;
        }
    });

    public Task<IReadOnlyList<ClipboardEntry>> GetHistoryAsync(int maxItems) => RunOnOwnThread<IReadOnlyList<ClipboardEntry>>(() =>
    {
        if (!IsHistoryEnabled)
        {
            return [];
        }

        try
        {
            var result = Wait(WinClipboard.GetHistoryItemsAsync());
            if (result.Status != ClipboardHistoryItemsResultStatus.Success)
            {
                return [];
            }

            _historyItems.Clear();
            var entries = new List<ClipboardEntry>();
            foreach (var item in result.Items.Take(maxItems))
            {
                _historyItems[item.Id] = item;
                if (Read(item.Content, item.Id, item.Timestamp) is { } entry)
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

    public Task<bool> CopyAgainAsync(string entryId) => RunOnOwnThread(() =>
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

    public Task<bool> SetTextAsync(string text) => RunOnOwnThread(() => SetContent(package => package.SetText(text)));

    public Task<bool> SetImageAsync(byte[] image) => RunOnOwnThread(() =>
    {
        var stream = new global::Windows.Storage.Streams.InMemoryRandomAccessStream();
        Wait(stream.WriteAsync(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(image)));
        stream.Seek(0);
        return SetContent(package => package.SetBitmap(global::Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(stream)));
    });

    /// <summary>Coloca um conteúdo na área de transferência e o mantém disponível após o Dynasland fechar.</summary>
    private bool SetContent(Action<DataPackage> fill)
    {
        try
        {
            var package = new DataPackage();
            fill(package);
            WinClipboard.SetContent(package);
            WinClipboard.Flush();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível copiar para a área de transferência.");
            return false;
        }
    }

    private void OnClipboardUpdate(IntPtr hwnd)
    {
        if (ClipboardPrivacy.IsPrivate(hwnd))
        {
            logger.LogDebug("Conteúdo privado copiado; ignorado.");
            return;
        }

        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A área de transferência WinRT exige thread STA em apps desktop (em MTA falha com RPC_E_WRONG_THREAD):
    /// toda leitura/escrita roda inteira na thread STA dedicada, aguardando as operações de forma síncrona.
    /// </summary>
    private Task<T> RunOnOwnThread<T>(Func<T> work) => _sta.Run(work);

    private static T Wait<T>(global::Windows.Foundation.IAsyncOperation<T> operation) => operation.AsTask().GetAwaiter().GetResult();

    private static T Wait<T, TProgress>(global::Windows.Foundation.IAsyncOperationWithProgress<T, TProgress> operation) =>
        operation.AsTask().GetAwaiter().GetResult();

    private static ClipboardEntry? Read(DataPackageView content, string id, DateTimeOffset timestamp)
    {
        if (content.Contains(StandardDataFormats.Text))
        {
            var text = Wait(content.GetTextAsync());
            if (text.Length > MaxTextLength)
            {
                text = text[..MaxTextLength];
            }

            return new ClipboardEntry(id, ClipboardEntryKind.Text, text, null, timestamp);
        }

        if (content.Contains(StandardDataFormats.Bitmap))
        {
            var bitmap = Wait(content.GetBitmapAsync());
            return new ClipboardEntry(id, ClipboardEntryKind.Image, null, ReadAllBytes(bitmap), timestamp);
        }

        if (content.Contains(StandardDataFormats.StorageItems))
        {
            var items = Wait(content.GetStorageItemsAsync());
            var names = string.Join(", ", items.Take(3).Select(i => i.Name));
            var label = items.Count > 3 ? $"{names} e mais {items.Count - 3}" : names;
            return new ClipboardEntry(id, ClipboardEntryKind.Other, label, null, timestamp);
        }

        return content.AvailableFormats.Count == 0 ? null : new ClipboardEntry(id, ClipboardEntryKind.Other, null, null, timestamp);
    }

    private const ulong MaxImageBytes = 16 * 1024 * 1024;

    private static byte[]? ReadAllBytes(global::Windows.Storage.Streams.IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        using var stream = Wait(reference.OpenReadAsync());
        if (stream.Size == 0 || stream.Size > MaxImageBytes)
        {
            return null;
        }

        using var reader = new global::Windows.Storage.Streams.DataReader(stream);
        var length = (uint)stream.Size;
        Wait(reader.LoadAsync(length));
        var bytes = new byte[length];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
