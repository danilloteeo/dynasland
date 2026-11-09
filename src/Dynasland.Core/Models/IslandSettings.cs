namespace Dynasland.Core.Models;

public enum AppTheme
{
    /// <summary>Segue o modo de cor de aplicativos do Windows.</summary>
    System,
    Dark,
    Light,
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

    /// <summary>Tema da janela de configurações (a ilha é sempre preta).</summary>
    public AppTheme Theme { get; init; } = AppTheme.System;

    /// <summary>Cor de destaque em hexadecimal (#RRGGBB).</summary>
    public string AccentColor { get; init; } = "#30D158";

    /// <summary>Opacidade do fundo da ilha (0,5 a 1).</summary>
    public double IslandOpacity { get; init; } = 1.0;

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

    // Sistema

    /// <summary>Procura novas versões ao iniciar (uma consulta ao GitHub, sem dados pessoais).</summary>
    public bool CheckForUpdates { get; init; } = true;
}
