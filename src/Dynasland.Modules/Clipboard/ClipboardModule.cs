using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Clipboard;

/// <summary>
/// Clipboard Manager: mostra o que foi copiado, guarda o histórico próprio (texto e imagem, conforme a
/// privacidade) e, no estado interativo, os itens recentes para copiar de novo. Histórico completo,
/// busca e favoritos ficam na Central. Com o histórico próprio desligado, usa o do Windows (Win+V).
/// </summary>
public sealed class ClipboardModule(
    IClipboardService clipboard,
    ClipboardHistory history,
    IIslandSettingsService settings,
    IEventBus bus,
    IUiDispatcher ui,
    IShortcutRegistry shortcuts,
    IGlobalShortcutRegistry globalShortcuts,
    IModuleOptions options,
    ILogger<ClipboardModule> logger) : IIslandModule
{
    public const string ModuleId = "clipboard";
    public const string CopiedActivityId = "clipboard.copied";
    public const string PanelActivityId = "clipboard.panel";

    private const int HistorySize = 4;
    private const int ThumbnailWidth = 96;
    private static readonly TimeSpan CopiedDisplayDuration = TimeSpan.FromSeconds(2);

    private readonly List<IDisposable> _registrations = [];
    private ClipboardContent? _content;

    public string Id => ModuleId;

    public string DisplayName => "Área de transferência";

    public string Description => "Mostra o que foi copiado e guarda um histórico com busca e favoritos. Ignora conteúdo marcado como privado e textos com aparência de senha, cartão ou chave.";

    public static readonly ModuleOption ShowCopied = ModuleOption.Toggle(
        "showCopied", "Mostrar o que foi copiado", "Expande a ilha por um instante a cada cópia. O histórico continua disponível pelo atalho.");

    public IReadOnlyList<ModuleOption> Options => [ShowCopied];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await clipboard.StartAsync(cancellationToken);
        _content = new ClipboardContent(ReloadHistoryAsync, () => { });
        clipboard.ContentChanged += OnContentChanged;
        _registrations.Add(shortcuts.Register(new IslandShortcut("clipboard.panel", ModuleId, Glyphs.Clipboard, "Área de transferência", OpenPanel)));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut(
            "clipboard.history", "Histórico da área de transferência", "Área de transferência",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'V'), OpenPanel)));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
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
            if (history.IsOwnCopy || _content is not { } content || await clipboard.GetCurrentAsync() is not { } entry)
            {
                return;
            }

            await history.CaptureAsync(entry);
            if (!options.IsOn(ModuleId, ShowCopied))
            {
                return;
            }

            var image = ImageDecoder.Decode(entry.Image, ThumbnailWidth);
            ui.Post(() => content.SetLatest(entry, image));
            bus.PostActivity(new IslandActivity(CopiedActivityId, ModuleId, ActivityKind.Transient, content) { Duration = CopiedDisplayDuration });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao processar a cópia.");
        }
    }

    private async Task ReloadHistoryAsync()
    {
        if (_content is not { } content)
        {
            return;
        }

        List<ClipboardHistoryItemViewModel> items;
        string emptyMessage;
        if (settings.Current.ClipboardHistoryEnabled)
        {
            var records = await history.ListAsync(limit: HistorySize);
            items = [.. records.Select(record => new ClipboardHistoryItemViewModel(
                new ClipboardEntry(record.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), record.Kind, record.Text, null, record.LastUsedAt),
                ImageDecoder.Decode(record.Thumbnail, ThumbnailWidth),
                () => history.CopyAsync(record.Id),
                record.IsFavorite))];
            emptyMessage = "Nada copiado ainda.";
        }
        else
        {
            // Histórico próprio desligado (privacidade): mostra o do Windows, se estiver ativo.
            var entries = await clipboard.GetHistoryAsync(HistorySize);
            items = [.. entries.Select(entry => new ClipboardHistoryItemViewModel(entry, ImageDecoder.Decode(entry.Image, ThumbnailWidth), () => CopyFromWindowsAsync(entry.Id)))];
            emptyMessage = clipboard.IsHistoryEnabled
                ? "Nada copiado ainda."
                : "O histórico está desligado nas configurações de privacidade.";
        }

        ui.Post(() => content.SetHistory(items, emptyMessage));
    }

    private Task CopyFromWindowsAsync(string entryId)
    {
        history.MarkOwnCopy();
        return clipboard.CopyAgainAsync(entryId);
    }
}
