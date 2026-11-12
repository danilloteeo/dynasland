using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Clipboard;

public sealed partial class ClipboardHistoryItemViewModel(ClipboardEntry entry, ImageSource? thumbnail, Func<Task> copyAgain, bool isFavorite = false) : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public string Preview => ClipboardContent.PreviewOf(entry);

    public ImageSource? Thumbnail { get; } = thumbnail;

    public bool HasThumbnail => Thumbnail is not null;

    public string Glyph => isFavorite ? Glyphs.FavoriteStarFill : ClipboardContent.GlyphOf(entry.Kind);

    public string TimeText => entry.Timestamp.ToLocalTime().ToString(entry.Timestamp.ToLocalTime().Date == DateTime.Today ? "HH:mm" : "dd/MM", PtBr);

    [ObservableProperty]
    private bool _justCopied;

    [RelayCommand]
    private async Task CopyAsync()
    {
        await copyAgain();
        JustCopied = true;
    }
}

/// <summary>Último conteúdo copiado (expandido) e histórico recente do Windows (interativo).</summary>
public sealed partial class ClipboardContent(Func<Task> reloadHistory, Action openAll) : ObservableObject, IActivityContent
{
    [RelayCommand]
    private void OpenAll() => openAll();

    private const int PreviewLength = 90;

    public ObservableCollection<ClipboardHistoryItemViewModel> History { get; } = [];

    [ObservableProperty]
    private string _latestPreview = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLatestImage))]
    private ImageSource? _latestImage;

    [ObservableProperty]
    private string _latestGlyph = Glyphs.Copy;

    [ObservableProperty]
    private string _historyMessage = string.Empty;

    public bool HasLatestImage => LatestImage is not null;

    public string Glyph => Glyphs.Clipboard;

    public static string PreviewOf(ClipboardEntry entry) => entry.Kind switch
    {
        ClipboardEntryKind.Image => "Imagem",
        _ when string.IsNullOrWhiteSpace(entry.Text) => "Conteúdo copiado",
        _ => Collapse(entry.Text!),
    };

    public static string GlyphOf(ClipboardEntryKind kind) => kind switch
    {
        ClipboardEntryKind.Image => Glyphs.Photo,
        ClipboardEntryKind.Text => Glyphs.Document,
        _ => Glyphs.Copy,
    };

    /// <summary>Uma linha só, sem quebras e espaços repetidos.</summary>
    private static string Collapse(string text)
    {
        var singleLine = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length > PreviewLength ? singleLine[..PreviewLength] + "…" : singleLine;
    }

    public void SetLatest(ClipboardEntry entry, ImageSource? image)
    {
        LatestPreview = PreviewOf(entry);
        LatestImage = image;
        LatestGlyph = GlyphOf(entry.Kind);
    }

    /// <param name="emptyMessage">Texto quando a lista está vazia (ou o histórico está desligado).</param>
    public void SetHistory(IReadOnlyList<ClipboardHistoryItemViewModel> items, string emptyMessage)
    {
        History.Clear();
        foreach (var item in items)
        {
            History.Add(item);
        }

        HistoryMessage = items.Count == 0 ? emptyMessage : string.Empty;
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(340, 56, 28),
        IslandState.Interactive => new IslandDimensions(440, 170, 34),
        _ => null,
    };

    public void OnPresentationChanged(IslandState? state)
    {
        if (state == IslandState.Interactive)
        {
            _ = reloadHistory();
        }
    }
}
