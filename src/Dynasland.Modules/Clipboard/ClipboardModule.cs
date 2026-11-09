using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Clipboard;

/// <summary>
/// Mostra rapidamente o que foi copiado e, no estado interativo, o histórico recente do Windows,
/// permitindo copiar um item de novo. O conteúdo vem do próprio Windows e não é armazenado.
/// </summary>
public sealed class ClipboardModule(
    IClipboardService clipboard,
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IShortcutRegistry shortcuts,
    IModuleOptions options,
    ILogger<ClipboardModule> logger) : IIslandModule
{
    public const string ModuleId = "clipboard";
    public const string CopiedActivityId = "clipboard.copied";
    public const string PanelActivityId = "clipboard.panel";

    private const int HistorySize = 4;
    private const int ThumbnailWidth = 96;
    private static readonly TimeSpan CopiedDisplayDuration = TimeSpan.FromSeconds(2);

    // A cópia feita pela própria ilha ("copiar de novo") também dispara o aviso do Windows.
    private static readonly TimeSpan OwnCopyWindow = TimeSpan.FromSeconds(1.5);

    private readonly Lock _lock = new();
    private ClipboardContent? _content;
    private IDisposable? _shortcut;
    private DateTimeOffset _lastOwnCopy = DateTimeOffset.MinValue;

    public string Id => ModuleId;

    public string DisplayName => "Área de transferência";

    public string Description => "Mostra o que foi copiado e o histórico recente do Windows (Win+V). Ignora conteúdo marcado como privado.";

    public static readonly ModuleOption ShowCopied = ModuleOption.Toggle(
        "showCopied", "Mostrar o que foi copiado", "Expande a ilha por um instante a cada cópia. O histórico continua disponível pelo atalho.");

    public IReadOnlyList<ModuleOption> Options => [ShowCopied];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await clipboard.StartAsync(cancellationToken);
        _content = new ClipboardContent(ReloadHistoryAsync);
        clipboard.ContentChanged += OnContentChanged;
        _shortcut = shortcuts.Register(new IslandShortcut("clipboard.panel", ModuleId, Glyphs.Clipboard, "Área de transferência", OpenPanel));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _shortcut?.Dispose();
        _shortcut = null;
        clipboard.ContentChanged -= OnContentChanged;
        await clipboard.StopAsync();
        _content = null;
    }

    public void OpenPanel()
    {
        if (_content is { } content)
        {
            bus.PostActivity(new IslandActivity(PanelActivityId, ModuleId, ActivityKind.Transient, content) { OpenInteractive = true });
        }
    }

    private async void OnContentChanged(object? sender, EventArgs e)
    {
        try
        {
            lock (_lock)
            {
                if (time.GetUtcNow() - _lastOwnCopy < OwnCopyWindow)
                {
                    return;
                }
            }

            if (!options.IsOn(ModuleId, ShowCopied) || _content is not { } content || await clipboard.GetCurrentAsync() is not { } entry)
            {
                return;
            }

            var image = ImageDecoder.Decode(entry.Image, ThumbnailWidth);
            ui.Post(() => content.SetLatest(entry, image));
            bus.PostActivity(new IslandActivity(CopiedActivityId, ModuleId, ActivityKind.Transient, content) { Duration = CopiedDisplayDuration });
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Falha ao ler a cópia.");
        }
    }

    private async Task ReloadHistoryAsync()
    {
        if (_content is not { } content)
        {
            return;
        }

        var entries = await clipboard.GetHistoryAsync(HistorySize);
        var items = entries
            .Select(entry => new ClipboardHistoryItemViewModel(entry, ImageDecoder.Decode(entry.Image, ThumbnailWidth), CopyAgainAsync))
            .ToList();
        var enabled = clipboard.IsHistoryEnabled;
        ui.Post(() => content.SetHistory(items, enabled));
    }

    private Task CopyAgainAsync(string entryId)
    {
        lock (_lock)
        {
            _lastOwnCopy = time.GetUtcNow();
        }

        return clipboard.CopyAgainAsync(entryId);
    }
}
