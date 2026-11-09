using Dynasland.Core.Models;

namespace Dynasland.Core.Layout;

public static class IslandPlacementCalculator
{
    /// <summary>
    /// Calcula a posição da janela centralizada horizontalmente no topo da área útil.
    /// Todos os valores estão em pixels físicos.
    /// </summary>
    public static PixelPoint TopCenter(PixelRect workArea, int windowWidth, double topMargin)
    {
        var x = workArea.Left + (int)Math.Round((workArea.Width - windowWidth) / 2.0);
        var y = workArea.Top + (int)Math.Round(Math.Max(0, topMargin));
        return new PixelPoint(x, y);
    }
}
