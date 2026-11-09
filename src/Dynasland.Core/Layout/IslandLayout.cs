using Dynasland.Core.Models;

namespace Dynasland.Core.Layout;

/// <summary>Dimensões da cápsula para cada estado da ilha.</summary>
public static class IslandLayout
{
    public static IslandDimensions Compact { get; } = new(190, 34, 17);

    public static IslandDimensions Expanded { get; } = new(340, 72, 30);

    public static IslandDimensions Interactive { get; } = new(400, 150, 36);

    public static IslandDimensions For(IslandState state) => state switch
    {
        IslandState.Compact => Compact,
        IslandState.Expanded => Expanded,
        IslandState.Interactive => Interactive,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };
}
