namespace Dynasland.Core.Models;

public enum AppTheme
{
    /// <summary>Segue o modo de cor de aplicativos do Windows.</summary>
    System,
    Dark,
    Light,
}

/// <summary>Fundo da ilha. Todos são escuros: o texto e os ícones continuam claros.</summary>
public enum IslandTheme
{
    Black,
    Graphite,
    Midnight,
    Aurora,
    Sunset,
    Forest,
    Steel,
    Wine,
    Copper,
    Ember,
    Magenta,

    /// <summary>Azul para magenta, na horizontal.</summary>
    Dusk,

    /// <summary>Tom escuro da cor de destaque escolhida.</summary>
    Accent,
}

/// <summary>O que a ilha compacta mostra em repouso (sem nenhuma atividade de outro módulo).</summary>
public enum IdleDisplay
{
    DateAndTime,
    TimeOnly,

    /// <summary>Clima agora e a hora; sem clima disponível, a data.</summary>
    Weather,

    /// <summary>Próximo compromisso e a hora; sem compromisso, a data.</summary>
    NextEvent,

    /// <summary>Velocidade de download e upload e a hora.</summary>
    Network,

    /// <summary>Ilha vazia.</summary>
    Nothing,
}

/// <summary>Posição horizontal da ilha no topo do monitor.</summary>
public enum IslandPosition
{
    Center,

    /// <summary>Junto à borda esquerda; a ilha cresce para a direita.</summary>
    Left,

    /// <summary>Junto à borda direita; a ilha cresce para a esquerda.</summary>
    Right,
}

/// <summary>Formato da ilha.</summary>
/// <summary>Borda da tela onde a ilha fica ancorada; o conteúdo cresce para longe dela.</summary>
public enum IslandEdge
{
    /// <summary>Embaixo, acima da barra de tarefas (padrão).</summary>
    Bottom,

    /// <summary>Em cima.</summary>
    Top,
}

public enum IslandShape
{
    /// <summary>Solta do topo (com a distância configurada) e com todos os cantos arredondados.</summary>
    Float,

    /// <summary>Encaixada na borda de cima da tela: cantos de cima retos, sem distância do topo.</summary>
    Notch,
}

/// <summary>Ação do duplo clique na ilha.</summary>
public enum DoubleClickAction
{
    /// <summary>Sem ação: o clique simples abre a ilha imediatamente.</summary>
    None,
    PlayPauseMedia,
    OpenSettings,
}

/// <summary>Preferências da aplicação. Imutável: alterações geram uma nova instância.</summary>
public sealed record IslandSettings
{
    public static IslandSettings Default { get; } = new();

    // Geral / comportamento

    /// <summary>Monitor escolhido pelo usuário; <c>null</c> segue o monitor principal.</summary>
    public string? MonitorDeviceName { get; init; }

    /// <summary>Distância do topo da área útil, em DIPs.</summary>
    public double TopMargin { get; init; } = 8;

    /// <summary>Esquerda, centro ou direita do topo da área útil.</summary>
    public IslandPosition Position { get; init; } = IslandPosition.Center;

    /// <summary>Flutuante (padrão) ou encaixada na borda de cima (ignora <see cref="TopMargin"/>).</summary>
    public IslandShape Shape { get; init; } = IslandShape.Float;

    /// <summary>Borda da tela (embaixo, padrão, ou em cima).</summary>
    public IslandEdge Edge { get; init; } = IslandEdge.Bottom;

    /// <summary>Módulos escondidos da dock (ids separados por vírgula; ver <c>DockVisibility</c>).</summary>
    public string DockHiddenModules { get; init; } = string.Empty;

    public bool IsVisible { get; init; } = true;

    /// <summary>Ao abrir o Dynasland, deixa a ilha oculta (só o ícone da bandeja).</summary>
    public bool StartHidden { get; init; }

    /// <summary>Oculta a ilha enquanto um aplicativo em tela cheia ocupa o monitor dela.</summary>
    public bool HideInFullscreen { get; init; } = true;

    // Animações

    public bool AnimationsEnabled { get; init; } = true;

    /// <summary>Multiplicador de velocidade das animações (1 = normal).</summary>
    public double AnimationSpeed { get; init; } = 1.0;

    /// <summary>Intensidade do efeito de mola ao expandir (0 = nenhum, 1 = normal, 2 = acentuado).</summary>
    public double AnimationIntensity { get; init; } = 1.0;

    /// <summary>Tempo sem interação até a ilha voltar ao estado compacto.</summary>
    public double ExpandedDurationSeconds { get; init; } = 6;

    /// <summary>Tempo sem interação até o painel interativo recolher (atalhos, controles).</summary>
    public TimeSpan ExpandedDuration => TimeSpan.FromSeconds(ExpandedDurationSeconds);

    // Aparência

    /// <summary>Tema da janela de configurações (o fundo da ilha vem de <see cref="IslandTheme"/>).</summary>
    public AppTheme Theme { get; init; } = AppTheme.System;

    /// <summary>Tema (fundo) da ilha.</summary>
    public IslandTheme IslandTheme { get; init; } = IslandTheme.Black;

    /// <summary>Enquanto uma mídia toca, o fundo da ilha usa a cor da capa em vez do tema.</summary>
    public bool IslandThemeFollowsMedia { get; init; }

    public IdleDisplay IdleDisplay { get; init; } = IdleDisplay.DateAndTime;

    /// <summary>Cor de destaque em hexadecimal (#RRGGBB).</summary>
    public string AccentColor { get; init; } = "#FFFFFF";

    /// <summary>Opacidade do fundo da ilha (0,5 a 1).</summary>
    public double IslandOpacity { get; init; } = 0.78;

    /// <summary>Intensidade da sombra da ilha (0 a 1).</summary>
    public double ShadowIntensity { get; init; } = 0.5;

    /// <summary>Arredondamento dos cantos (0,3 = mais quadrado, 1 = cápsula).</summary>
    public double Roundness { get; init; } = 1.0;

    /// <summary>Largura da ilha compacta (largura mínima).</summary>
    public double CompactWidth { get; init; } = 190;

    /// <summary>Altura da ilha compacta.</summary>
    public double CompactHeight { get; init; } = 34;

    /// <summary>Largura máxima da ilha expandida.</summary>
    public double MaxWidth { get; init; } = 420;

    // Comportamento

    /// <summary>Clicar fora da ilha aberta a recolhe.</summary>
    public bool CollapseOnClickOutside { get; init; } = true;

    public DoubleClickAction DoubleClickAction { get; init; } = DoubleClickAction.None;

    /// <summary>Módulo fixado: sua atividade contínua é sempre o indicador do compacto. <c>null</c> = por prioridade.</summary>
    public string? PinnedModuleId { get; init; }

    // Privacidade e armazenamento

    /// <summary>Guarda o que é copiado no histórico próprio do Dynasland.</summary>
    public bool ClipboardHistoryEnabled { get; init; } = true;

    /// <summary>Inclui imagens copiadas no histórico.</summary>
    public bool ClipboardStoreImages { get; init; } = true;

    /// <summary>Itens mantidos no histórico da área de transferência (favoritos não contam).</summary>
    public int ClipboardMaxItems { get; init; } = 200;

    public bool NotificationHistoryEnabled { get; init; } = true;

    /// <summary>Dias que as notificações ficam no histórico.</summary>
    public int NotificationHistoryDays { get; init; } = 7;

    /// <summary>Pontos na ilha compacta enquanto algum aplicativo usa o microfone (laranja) ou a câmera (verde).</summary>
    public bool ShowPrivacyIndicators { get; init; } = true;

    // Life Center

    /// <summary>Local da previsão do tempo (formato de <c>WeatherLocation.Serialize</c>); <c>null</c> = clima desligado.</summary>
    public string? WeatherLocation { get; init; }

    /// <summary>Última versão cujas novidades o usuário já viu; <c>null</c> = nenhuma.</summary>
    public string? LastSeenVersion { get; init; }

    /// <summary>Client ID do app do Spotify criado pelo usuário (para ler a fila); <c>null</c> = desligado.</summary>
    public string? SpotifyClientId { get; init; }

    // Sistema

    /// <summary>Procura novas versões ao iniciar (uma consulta ao GitHub, sem dados pessoais).</summary>
    public bool CheckForUpdates { get; init; } = true;
}
