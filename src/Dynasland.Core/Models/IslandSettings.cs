namespace Dynasland.Core.Models;

/// <summary>Preferências da ilha. Imutável: alterações geram uma nova instância.</summary>
public sealed record IslandSettings
{
    public static IslandSettings Default { get; } = new();

    /// <summary>Monitor escolhido pelo usuário; <c>null</c> segue o monitor principal.</summary>
    public string? MonitorDeviceName { get; init; }

    /// <summary>Distância do topo da área útil, em DIPs.</summary>
    public double TopMargin { get; init; } = 8;

    public bool IsVisible { get; init; } = true;

    public bool AnimationsEnabled { get; init; } = true;

    /// <summary>Multiplicador de velocidade das animações (1 = normal).</summary>
    public double AnimationSpeed { get; init; } = 1.0;

    /// <summary>Tempo sem interação até a ilha voltar ao estado compacto.</summary>
    public double ExpandedDurationSeconds { get; init; } = 6;

    /// <summary>Tempo sem interação até o painel interativo recolher (atalhos, controles).</summary>
    public TimeSpan ExpandedDuration => TimeSpan.FromSeconds(ExpandedDurationSeconds);
}
