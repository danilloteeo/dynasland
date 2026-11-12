using System.Text.Json;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Infrastructure.Market;
using Dynasland.Modules.KeyLocks;
using Dynasland.Modules.Quotes;
using Dynasland.Modules.Scores;

namespace Dynasland.Tests.Modules;

public class MarketModulesTests
{
    private const string QuotesJson = """
        {
          "USDBRL": { "bid": "5.3212", "pctChange": "0.42" },
          "EURBRL": { "bid": "5.7001", "pctChange": "-0.10" },
          "BTCBRL": { "bid": "345210.55", "pctChange": "1.8" }
        }
        """;

    private const string ScoresJson = """
        {
          "events": [
            {
              "id": "1",
              "status": { "type": { "state": "in", "shortDetail": "Q3 5:10" } },
              "competitions": [ { "competitors": [
                { "homeAway": "home", "score": "88", "team": { "abbreviation": "BOS", "shortDisplayName": "Celtics" } },
                { "homeAway": "away", "score": "91", "team": { "abbreviation": "LAL", "shortDisplayName": "Lakers" } } ] } ]
            },
            {
              "id": "2",
              "status": { "type": { "state": "pre", "shortDetail": "9:00 PM EST" } },
              "competitions": [ { "competitors": [
                { "homeAway": "home", "score": "0", "team": { "abbreviation": "MIA", "displayName": "Miami Heat" } },
                { "homeAway": "away", "score": "0", "team": { "abbreviation": "NYK", "displayName": "New York Knicks" } } ] } ]
            },
            {
              "id": "3",
              "status": { "type": { "state": "post", "shortDetail": "Final" } },
              "competitions": [ { "competitors": [
                { "homeAway": "home", "score": "110", "team": { "abbreviation": "DEN", "shortDisplayName": "Nuggets" } },
                { "homeAway": "away", "score": "104", "team": { "abbreviation": "PHX", "shortDisplayName": "Suns" } } ] } ]
            }
          ]
        }
        """;

    [Fact]
    public void Quotes_are_read_in_the_fixed_order_with_price_and_change()
    {
        using var document = JsonDocument.Parse(QuotesJson);

        var quotes = AwesomeApiQuotesProvider.Map(document.RootElement);

        Assert.Equal(["USD", "EUR", "BTC"], quotes.Select(q => q.Code));
        Assert.Equal(5.3212m, quotes[0].Price);
        Assert.Equal(-0.10m, quotes[1].ChangePercent);
    }

    [Fact]
    public void A_missing_or_malformed_pair_is_skipped_instead_of_failing()
    {
        using var document = JsonDocument.Parse("""{ "USDBRL": { "bid": "abc", "pctChange": "1" }, "EURBRL": { "bid": "5.5", "pctChange": "0" } }""");

        var quotes = AwesomeApiQuotesProvider.Map(document.RootElement);

        Assert.Equal(["EUR"], quotes.Select(q => q.Code));
    }

    [Fact]
    public void Quote_text_uses_reais_and_a_signed_percentage()
    {
        Assert.Equal("+0,42%", QuotesContent.FormatChange(0.42m));
        Assert.Equal("-0,10%", QuotesContent.FormatChange(-0.10m));
        Assert.Contains("5,32", QuotesContent.FormatPrice(5.3212m));
        Assert.Contains("345.211", QuotesContent.FormatPrice(345210.55m));
    }

    [Fact]
    public void Games_are_mapped_with_state_and_hidden_scores_before_the_start()
    {
        using var document = JsonDocument.Parse(ScoresJson);

        var games = EspnScoresProvider.Map(document.RootElement);

        Assert.Equal(3, games.Count);
        Assert.Equal(GameState.Live, games[0].State);
        Assert.Equal(91, games[0].AwayScore);
        Assert.Equal("Q3 5:10", games[0].StatusText);
        Assert.Equal(GameState.Scheduled, games[1].State);
        Assert.Null(games[1].HomeScore);
        Assert.Equal("New York Knicks", games[1].AwayName);
        Assert.Equal(GameState.Final, games[2].State);
        Assert.Equal("Final", games[2].StatusText);
    }

    [Fact]
    public void A_response_without_events_gives_an_empty_scoreboard()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Empty(EspnScoresProvider.Map(document.RootElement));
    }

    [Fact]
    public void The_scoreboard_lists_live_games_first_then_upcoming_then_finished()
    {
        using var document = JsonDocument.Parse(ScoresJson);
        var games = EspnScoresProvider.Map(document.RootElement);
        var content = new ScoresContent(() => Task.CompletedTask);

        content.SetGames([games[2], games[1], games[0]], DateTimeOffset.Now);

        Assert.Equal(["LAL @ BOS", "NYK @ MIA", "PHX @ DEN"], content.Games.Select(g => g.Matchup));
        Assert.Equal("91 – 88", content.Games[0].ScoreText);
        Assert.Equal(string.Empty, content.Games[1].ScoreText);
        Assert.False(content.IsEmpty);
    }

    [Fact]
    public void An_empty_scoreboard_says_there_are_no_games()
    {
        var content = new ScoresContent(() => Task.CompletedTask);

        content.SetGames([], DateTimeOffset.Now);

        Assert.True(content.IsEmpty);
        Assert.Equal("Sem jogos hoje", content.Status);
    }

    [Fact]
    public void Quote_rows_mark_rises_and_falls()
    {
        var content = new QuotesContent(() => Task.CompletedTask);

        content.SetQuotes([new Quote("USD", "Dólar", 5.3m, 0.4m), new Quote("EUR", "Euro", 5.7m, -0.1m)], DateTimeOffset.Now);

        Assert.True(content.Quotes[0].IsUp);
        Assert.True(content.Quotes[1].IsDown);
        Assert.False(content.HasError);
    }

    [Fact]
    public void A_failed_refresh_keeps_the_previous_quotes_and_shows_the_error()
    {
        var content = new QuotesContent(() => Task.CompletedTask);
        content.SetQuotes([new Quote("USD", "Dólar", 5.3m, 0.4m)], DateTimeOffset.Now);

        content.SetError("Sem conexão");

        Assert.True(content.HasError);
        Assert.Equal("Sem conexão", content.Status);
        Assert.Single(content.Quotes);
    }

    [Fact]
    public void Key_lock_notice_names_the_key_and_its_state()
    {
        var on = new KeyLockContent("Caps Lock", true);
        var off = new KeyLockContent("Num Lock", false);

        Assert.Equal("Ligado", on.StateText);
        Assert.Equal("Desligado", off.StateText);
        Assert.Equal(300, on.GetPreferredDimensions(IslandState.Expanded)?.Width);
    }
}
