using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.ViewModels.Hub;

public sealed partial class ClipboardItemViewModel(ClipboardRecord record) : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public long Id => record.Id;

    public bool IsImage => record.Kind == ClipboardEntryKind.Image;

    public string Preview => IsImage ? "Imagem" : string.Join(' ', (record.Text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) is var line && line.Length > 160 ? line[..160] + "…" : line;

    public string FullText => record.Text ?? string.Empty;

    public ImageSource? Thumbnail { get; } = ImageDecoder.Decode(record.Thumbnail, 160);

    public string TimeText => record.LastUsedAt.ToLocalTime().ToString(record.LastUsedAt.ToLocalTime().Date == DateTime.Today ? "'Hoje,' HH:mm" : "dd/MM/yyyy HH:mm", PtBr);

    public string DetailText => IsImage ? TimeText : $"{TimeText} · {FullText.Length:N0} caracteres";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteGlyph), nameof(FavoriteToolTip))]
    private bool _isFavorite = record.IsFavorite;

    public string FavoriteGlyph => IsFavorite ? Glyphs.FavoriteStarFill : Glyphs.FavoriteStar;

    public string FavoriteToolTip => IsFavorite ? "Remover dos favoritos" : "Favoritar";

    /// <summary>Imagem em tamanho maior para a pré-visualização (decodificada só ao selecionar).</summary>
    public ImageSource? LoadPreviewImage() => IsImage ? ImageDecoder.Decode(record.Image, 720) : null;
}

/// <summary>Página da Central com o histórico da área de transferência: busca, favoritos, copiar, excluir.</summary>
public sealed partial class ClipboardPageViewModel : ObservableObject, IHubPage
{
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly IClipboardHistory _history;
    private readonly IIslandSettingsService _settings;
    private readonly IUiDispatcher _ui;
    private readonly ILogger<ClipboardPageViewModel> _logger;
    private CancellationTokenSource? _searchDelay;

    public ClipboardPageViewModel(IClipboardHistory history, IIslandSettingsService settings, IUiDispatcher ui, ILogger<ClipboardPageViewModel> logger)
    {
        _history = history;
        _settings = settings;
        _ui = ui;
        _logger = logger;
        _historyEnabled = settings.Current.ClipboardHistoryEnabled;
        history.Changed += OnHistoryChanged;
    }

    public HubPage Page => HubPage.Clipboard;

    /// <summary>Confirmação da limpeza, fornecida pela janela.</summary>
    public Func<string, bool>? Confirm { get; set; }

    public ObservableCollection<ClipboardItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectedIsImage))]
    private ClipboardItemViewModel? _selected;

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private bool _historyEnabled;

    [ObservableProperty]
    private string? _copiedMessage;

    public bool HasSelection => Selected is not null;

    public bool SelectedIsImage => Selected?.IsImage == true;

    public bool IsEmpty => Items.Count == 0;

    public async Task LoadAsync()
    {
        try
        {
            var records = await _history.ListAsync(string.IsNullOrWhiteSpace(SearchText) ? null : SearchText, FavoritesOnly, 500);
            var selectedId = Selected?.Id;
            var items = records.Select(r => new ClipboardItemViewModel(r)).ToList();
            _ui.Post(() =>
            {
                Items.Clear();
                foreach (var item in items)
                {
                    Items.Add(item);
                }

                Selected = Items.FirstOrDefault(i => i.Id == selectedId) ?? Items.FirstOrDefault();
                StatusText = items.Count == 1 ? "1 item" : $"{items.Count} itens";
                EmptyText = !HistoryEnabled ? "O histórico está desligado. Ligue acima para guardar o que você copiar."
                    : !string.IsNullOrWhiteSpace(SearchText) ? "Nada encontrado para essa busca."
                    : FavoritesOnly ? "Nenhum favorito ainda. Use a estrela para guardar itens que você usa sempre."
                    : "Nada copiado ainda.";
                OnPropertyChanged(nameof(IsEmpty));
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar o histórico da área de transferência.");
            _ui.Post(() => EmptyText = "Não foi possível carregar o histórico.");
        }
    }

    partial void OnSearchTextChanged(string value) => ReloadSoon();

    partial void OnFavoritesOnlyChanged(bool value) => _ = LoadAsync();

    partial void OnSelectedChanged(ClipboardItemViewModel? value) => PreviewImage = value?.LoadPreviewImage();

    partial void OnHistoryEnabledChanged(bool value)
    {
        if (_settings.Current.ClipboardHistoryEnabled != value)
        {
            _ = _settings.UpdateAsync(s => s with { ClipboardHistoryEnabled = value });
        }

        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task CopyAsync(ClipboardItemViewModel? item)
    {
        if ((item ?? Selected) is not { } target)
        {
            return;
        }

        CopiedMessage = await _history.CopyAsync(target.Id) ? "Copiado" : "Não foi possível copiar";
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(ClipboardItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsFavorite = !item.IsFavorite;
        await _history.SetFavoriteAsync(item.Id, item.IsFavorite);
    }

    [RelayCommand]
    private async Task DeleteAsync(ClipboardItemViewModel? item)
    {
        if (item is not null)
        {
            await _history.DeleteAsync(item.Id);
        }
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (Confirm?.Invoke("Apagar todo o histórico da área de transferência?\n\nOs favoritos são mantidos.") == false)
        {
            return;
        }

        await _history.ClearAsync(keepFavorites: true);
    }

    public void Dispose()
    {
        _history.Changed -= OnHistoryChanged;
        _searchDelay?.Cancel();
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => _ = LoadAsync();

    private void ReloadSoon()
    {
        _searchDelay?.Cancel();
        var cts = _searchDelay = new CancellationTokenSource();
        _ = Task.Delay(SearchDelay, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                _ = LoadAsync();
            }
        }, TaskScheduler.Default);
    }
}
