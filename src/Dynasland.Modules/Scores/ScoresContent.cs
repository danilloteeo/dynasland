using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Scores;

/// <summary>Uma linha do painel: "VISITANTE @ CASA", o placar e o estado do jogo.</summary>
public sealed record ScoreItem(string Matchup, string ScoreText, string StatusText, bool IsLive);

/// <summary>
/// Painel do placar da NBA. Só busca dados enquanto está visível: ao abrir e depois a cada 30 segundos.
/// </summary>
public sealed partial class ScoresContent(Func<Task> refresh) : ObservableObject, IActivityContent
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    private DispatcherTimer? _timer;

    public System.Collections.ObjectModel.ObservableCollection<ScoreItem> Games { get; } = [];

    [ObservableProperty]
    private string _status = "Carregando…";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isEmpty;

    public string Glyph => VectorIcons.Ball;

    public static ScoreItem ToItem(GameScore game) => new(
        $"{game.AwayAbbreviation} @ {game.HomeAbbreviation}",
        game.AwayScore is { } away && game.HomeScore is { } home ? $"{away} – {home}" : string.Empty,
        game.StatusText,
        game.State == GameState.Live);

    /// <summary>Atualiza a lista de jogos. Chamar na thread de UI.</summary>
    public void SetGames(IReadOnlyList<GameScore> games, DateTimeOffset at)
    {
        Games.Clear();
        foreach (var game in games.OrderBy(g => g.State == GameState.Live ? 0 : g.State == GameState.Scheduled ? 1 : 2))
        {
            Games.Add(ToItem(game));
        }

        HasError = false;
        IsEmpty = Games.Count == 0;
        Status = IsEmpty ? "Sem jogos hoje" : $"NBA · {at.ToString("HH:mm", CultureInfo.GetCultureInfo("pt-BR"))}";
    }

    /// <summary>Mostra o erro sem apagar os jogos anteriores.</summary>
    public void SetError(string message)
    {
        HasError = true;
        Status = message;
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state == IslandState.Interactive ? new IslandDimensions(440, 196, 32) : null;

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
