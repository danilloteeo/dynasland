namespace Dynasland.Core.Interfaces;

/// <summary>Cotação de uma moeda ou criptomoeda em reais.</summary>
/// <param name="Code">Código curto (USD, EUR, BTC).</param>
/// <param name="Name">Nome para exibir ("Dólar").</param>
/// <param name="Price">Preço em reais.</param>
/// <param name="ChangePercent">Variação do dia, em % (negativa quando cai).</param>
public sealed record Quote(string Code, string Name, decimal Price, decimal ChangePercent);

/// <summary>Fonte de cotações (ex.: AwesomeAPI). Pode lançar <see cref="HttpRequestException"/> sem conexão.</summary>
public interface IQuotesProvider
{
    string Name { get; }

    Task<IReadOnlyList<Quote>> GetQuotesAsync(CancellationToken cancellationToken = default);
}

public enum GameState
{
    Scheduled,
    Live,
    Final,
}

/// <summary>Um jogo do placar. Os pontos são <c>null</c> enquanto o jogo não começou.</summary>
public sealed record GameScore(
    string Id,
    string AwayName,
    string AwayAbbreviation,
    int? AwayScore,
    string HomeName,
    string HomeAbbreviation,
    int? HomeScore,
    GameState State,
    string StatusText);

/// <summary>Fonte do placar de jogos do dia (ex.: ESPN, NBA). Pode lançar <see cref="HttpRequestException"/> sem conexão.</summary>
public interface IScoresProvider
{
    string Name { get; }

    Task<IReadOnlyList<GameScore>> GetGamesAsync(CancellationToken cancellationToken = default);
}

public sealed record KeyLockState(bool CapsLock, bool NumLock);

public sealed class KeyLockChangedEventArgs(string key, bool isOn, KeyLockState state) : EventArgs
{
    /// <summary>"Caps Lock" ou "Num Lock".</summary>
    public string Key { get; } = key;

    public bool IsOn { get; } = isOn;

    public KeyLockState State { get; } = state;
}

/// <summary>Estado das teclas Caps Lock e Num Lock, com aviso quando mudam.</summary>
public interface IKeyLockService
{
    KeyLockState Current { get; }

    /// <summary>Disparado fora da thread de UI.</summary>
    event EventHandler<KeyLockChangedEventArgs>? Changed;

    void Start();

    void Stop();
}
