namespace Dynasland.Core.Interfaces;

// Gaming Monitor: detecção de jogos e medição de FPS.

/// <summary>Jogo em primeiro plano.</summary>
public sealed record GameSession(int ProcessId, string Name, string ExecutablePath, DateTimeOffset StartedAt);

/// <summary>
/// Detecta quando um jogo está em primeiro plano: executável em pastas de lojas de jogos (Steam, Epic,
/// Xbox, GOG, Riot, Ubisoft, EA, Battle.net) ou programa em tela cheia que não seja navegador, player
/// de vídeo ou apresentação. É uma heurística, não uma lista universal.
/// </summary>
public interface IGameDetector
{
    GameSession? Current { get; }

    /// <summary>Jogo começou (valor) ou terminou/saiu do primeiro plano (<c>null</c>). Qualquer thread.</summary>
    event EventHandler<GameSession?>? Changed;

    void Start();

    void Stop();
}

/// <summary>
/// Quadros por segundo de um processo, a partir dos eventos de apresentação do DirectX (ETW), sem injetar
/// nada no jogo (não interfere em anticheat). Exige executar como administrador ou estar no grupo
/// "Usuários de log de desempenho" do Windows.
/// </summary>
public interface IFpsProvider
{
    /// <summary>Motivo de indisponibilidade (sem permissão, por exemplo); <c>null</c> se funcionando.</summary>
    string? UnavailableReason { get; }

    /// <returns><c>false</c> se não foi possível começar (ver <see cref="UnavailableReason"/>).</returns>
    bool Start();

    void Stop();

    /// <summary>FPS médio do último segundo; <c>null</c> se o processo não apresentou quadros.</summary>
    double? GetFps(int processId);
}

/// <summary>Modo gamer ligado/desligado, com o que deve mudar no restante do aplicativo.</summary>
public sealed record GamingModeChangedEvent(bool Active, GameSession? Game, bool MuteNotifications, bool HideIsland);
