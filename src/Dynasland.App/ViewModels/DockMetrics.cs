using Dynasland.Core.Models;

namespace Dynasland.App.ViewModels;

/// <summary>Medidas da dock (DIPs): decidem a largura da cápsula no estado compacto.</summary>
public static class DockMetrics
{
    /// <summary>Largura de cada ícone, com o espaço entre eles.</summary>
    public const double ItemWidth = 40;

    /// <summary>Altura da dock (a linha do rodapé da cápsula).</summary>
    public const double Height = 48;

    /// <summary>Margem interna dos dois lados da dock.</summary>
    public const double Padding = 12;

    /// <summary>Espaço extra para o texto curto de uma atividade (ex.: música) ao lado dos ícones.</summary>
    public const double ActivePillExtra = 150;

    public const double MinimumWidth = 96;

    public static double Width(int itemCount, bool hasActivePill) => Width(itemCount, hasActivePill ? ActivePillExtra : 0);

    /// <summary>Largura da dock com <paramref name="extra"/> DIPs de texto ao lado dos ícones.</summary>
    public static double Width(int itemCount, double extra) =>
        Math.Max(MinimumWidth, Padding * 2 + itemCount * ItemWidth + extra);

    /// <summary>Espaço do texto de repouso (Aparência › Ilha em repouso): cada opção mostra um texto de tamanho diferente.</summary>
    public static double IdleExtra(IdleDisplay display) => display switch
    {
        IdleDisplay.Nothing => 0,
        IdleDisplay.TimeOnly => 76,
        IdleDisplay.DateAndTime => 168,
        IdleDisplay.Weather => 190,
        IdleDisplay.NextEvent => 220,
        IdleDisplay.Network => 190,
        _ => ActivePillExtra,
    };
}
