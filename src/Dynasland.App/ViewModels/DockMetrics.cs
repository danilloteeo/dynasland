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

    /// <summary>Espaço extra da pílula ativa para o texto curto da atividade.</summary>
    public const double ActivePillExtra = 96;

    public const double MinimumWidth = 96;

    public static double Width(int itemCount, bool hasActivePill) =>
        Math.Max(MinimumWidth, Padding * 2 + itemCount * ItemWidth + (hasActivePill ? ActivePillExtra : 0));
}
