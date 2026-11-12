using System.Globalization;
using System.Text.Json;
using Dynasland.Core.Interfaces;

namespace Dynasland.Infrastructure.Market;

/// <summary>
/// Placar da NBA pelo serviço público de placares da ESPN (sem chave). Pede os jogos do dia
/// apenas quando o painel está aberto.
/// </summary>
public sealed class EspnScoresProvider(HttpClient http) : IScoresProvider
{
    private const string Url = "https://site.api.espn.com/apis/site/v2/sports/basketball/nba/scoreboard";

    public string Name => "ESPN";

    public async Task<IReadOnlyList<GameScore>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = await http.GetStreamAsync(Url, cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return Map(document.RootElement);
    }

    /// <summary>Converte a resposta da API (separado para testes com JSON gravado).</summary>
    internal static IReadOnlyList<GameScore> Map(JsonElement root)
    {
        var games = new List<GameScore>();
        if (!root.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
        {
            return games;
        }

        foreach (var evt in events.EnumerateArray())
        {
            if (TryMap(evt) is { } game)
            {
                games.Add(game);
            }
        }

        return games;
    }

    private static GameScore? TryMap(JsonElement evt)
    {
        if (!evt.TryGetProperty("competitions", out var competitions) || competitions.GetArrayLength() == 0
            || !competitions[0].TryGetProperty("competitors", out var competitors))
        {
            return null;
        }

        JsonElement? home = null;
        JsonElement? away = null;
        foreach (var competitor in competitors.EnumerateArray())
        {
            var side = Text(competitor, "homeAway");
            if (side == "home")
            {
                home = competitor;
            }
            else if (side == "away")
            {
                away = competitor;
            }
        }

        if (home is null || away is null)
        {
            return null;
        }

        var type = evt.TryGetProperty("status", out var status) && status.TryGetProperty("type", out var t) ? t : default;
        var state = Text(type, "state") switch
        {
            "in" => GameState.Live,
            "post" => GameState.Final,
            _ => GameState.Scheduled,
        };

        var started = state != GameState.Scheduled;
        return new GameScore(
            Text(evt, "id"),
            TeamName(away.Value), TeamAbbreviation(away.Value), started ? Score(away.Value) : null,
            TeamName(home.Value), TeamAbbreviation(home.Value), started ? Score(home.Value) : null,
            state,
            StatusText(type, state, Text(evt, "date")));
    }

    private static string StatusText(JsonElement type, GameState state, string startUtc)
    {
        var detail = Text(type, "shortDetail");
        return state switch
        {
            GameState.Final => "Final",
            GameState.Live => detail.Length > 0 ? detail : "Ao vivo",
            // Jogo que ainda não começou: dia e hora no fuso do computador, não "7:00 PM EST".
            _ => DateTimeOffset.TryParse(startUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start)
                ? start.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)
                : detail.Length > 0 ? detail : "Em breve",
        };
    }

    private static int? Score(JsonElement competitor) =>
        int.TryParse(Text(competitor, "score"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string TeamName(JsonElement competitor) =>
        competitor.TryGetProperty("team", out var team) ? FirstNonEmpty(Text(team, "shortDisplayName"), Text(team, "displayName")) : string.Empty;

    private static string TeamAbbreviation(JsonElement competitor) =>
        competitor.TryGetProperty("team", out var team) ? Text(team, "abbreviation") : string.Empty;

    private static string FirstNonEmpty(string first, string second) => first.Length > 0 ? first : second;

    private static string Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
