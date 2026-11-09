using Dynasland.Core.Models;

namespace Dynasland.Core.Layout;

/// <summary>Aplica as preferências de aparência (largura, altura, arredondamento) às dimensões pedidas.</summary>
public static class IslandAppearance
{
    public static IslandDimensions Adjust(IslandDimensions requested, IslandState state, IslandSettings settings)
    {
        double width, height;
        if (state == IslandState.Compact)
        {
            // Indicadores (ex.: mídia) podem pedir mais largura que o mínimo configurado.
            var extra = Math.Max(0, requested.Width - IslandLayout.Compact.Width);
            width = settings.CompactWidth + extra;
            height = settings.CompactHeight;
        }
        else
        {
            width = requested.Width;
            height = requested.Height;
        }

        width = Math.Min(width, settings.MaxWidth);

        // Com arredondamento 1, a compacta é uma cápsula perfeita (raio = metade da altura).
        var baseRadius = state == IslandState.Compact ? height / 2 : requested.CornerRadius;
        var radius = Math.Min(height / 2, baseRadius * settings.Roundness);
        return new IslandDimensions(width, height, radius);
    }
}
