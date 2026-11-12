using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Quotes;

/// <summary>Uma linha do painel: moeda, preço em reais e variação do dia.</summary>
public sealed record QuoteItem(string Name, string PriceText, string ChangeText, bool IsUp, bool IsDown);

/// <summary>
/// Painel de cotações (dólar, euro e bitcoin em reais). Só busca dados enquanto está visível:
/// ao abrir e depois a cada minuto.
/// </summary>
public sealed partial class QuotesContent(Func<Task> refresh) : ObservableObject, IActivityContent
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    private DispatcherTimer? _timer;

    public System.Collections.ObjectModel.ObservableCollection<QuoteItem> Quotes { get; } = [];

    [ObservableProperty]
    private string _status = "Carregando…";

    [ObservableProperty]
    private bool _hasError;

    public string Glyph => VectorIcons.Trend;

    public static string FormatPrice(decimal price) =>
        price >= 1000 ? price.ToString("C0", Culture) : price.ToString("C2", Culture);

    public static string FormatChange(decimal change) =>
        (change > 0 ? "+" : string.Empty) + change.ToString("0.00", Culture) + "%";

    /// <summary>Atualiza as linhas e o texto de "atualizado às". Chamar na thread de UI.</summary>
    public void SetQuotes(IReadOnlyList<Quote> quotes, DateTimeOffset at)
    {
        Quotes.Clear();
        foreach (var quote in quotes)
        {
            Quotes.Add(new QuoteItem(quote.Name, FormatPrice(quote.Price), FormatChange(quote.ChangePercent), quote.ChangePercent > 0, quote.ChangePercent < 0));
        }

        HasError = false;
        Status = quotes.Count == 0 ? "Sem cotações agora" : $"Atualizado às {at.ToString("HH:mm", Culture)}";
    }

    /// <summary>Mostra o erro sem apagar os valores anteriores.</summary>
    public void SetError(string message)
    {
        HasError = true;
        Status = message;
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state == IslandState.Interactive ? new IslandDimensions(440, 148, 32) : null;

    public void OnPresentationChanged(IslandState? state)
    {
        if (state is null)
        {
            _timer?.Stop();
            return;
        }

        _timer ??= new DispatcherTimer(RefreshInterval, DispatcherPriority.Background, (_, _) => _ = refresh(), Dispatcher.CurrentDispatcher);
        _timer.Start();
    }
}
