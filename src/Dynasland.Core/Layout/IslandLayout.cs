using Dynasland.Core.Models;

namespace Dynasland.Core.Layout;

/// <summary>Dimensões da cápsula para cada estado da ilha.</summary>
public static class IslandLayout
{
    public static IslandDimensions Compact { get; } = new(190, 34, 17);

    public static IslandDimensions Expanded { get; } = new(340, 72, 30);

    public static IslandDimensions Interactive { get; } = new(400, 150, 36);

    /// <summary>Limites aceitos para tamanhos pedidos pelos módulos (a janela comporta o máximo + sombra).</summary>
    public static IslandDimensions Minimum { get; } = new(120, 30, 0);

    public static IslandDimensions Maximum { get; } = new(420, 170, 0);

    public static IslandDimensions For(IslandState state) => state switch
    {
        IslandState.Compact => Compact,
        IslandState.Expanded => Expanded,
        IslandState.Interactive => Interactive,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static IslandDimensions Clamp(IslandDimensions dimensions)
    {
        var width = Math.Clamp(dimensions.Width, Minimum.Width, Maximum.Width);
        var height = Math.Clamp(dimensions.Height, Minimum.Height, Maximum.Height);
        var radius = Math.Clamp(dimensions.CornerRadius, 0, height / 2);
        return new IslandDimensions(width, height, radius);
    }
}
