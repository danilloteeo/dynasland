using System.Globalization;
using System.Text.Json;
using Dynasland.Core.Interfaces;

namespace Dynasland.Infrastructure.Market;

/// <summary>
/// Cotações pela AwesomeAPI (https://docs.awesomeapi.com.br): gratuita e sem chave.
/// Pede só dólar, euro e bitcoin em reais, e apenas quando o painel está aberto.
/// </summary>
public sealed class AwesomeApiQuotesProvider(HttpClient http) : IQuotesProvider
{
    private const string Url = "https://economia.awesomeapi.com.br/last/USD-BRL,EUR-BRL,BTC-BRL";

    private static readonly (string Key, string Code, string Name)[] Pairs =
    [
        ("USDBRL", "USD", "Dólar"),
        ("EURBRL", "EUR", "Euro"),
        ("BTCBRL", "BTC", "Bitcoin"),
    ];

    public string Name => "AwesomeAPI";

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = await http.GetStreamAsync(Url, cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return Map(document.RootElement);
    }

    /// <summary>Converte a resposta da API (separado para testes com JSON gravado).</summary>
    internal static IReadOnlyList<Quote> Map(JsonElement root)
    {
        var quotes = new List<Quote>();
        foreach (var (key, code, name) in Pairs)
        {
            if (!root.TryGetProperty(key, out var item)
                || !TryReadDecimal(item, "bid", out var price)
                || !TryReadDecimal(item, "pctChange", out var change))
            {
                continue;
            }

            quotes.Add(new Quote(code, name, price, change));
        }

        return quotes;
    }

    private static bool TryReadDecimal(JsonElement item, string property, out decimal value)
    {
        value = 0;
        return item.TryGetProperty(property, out var raw)
            && raw.ValueKind == JsonValueKind.String
            && decimal.TryParse(raw.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
