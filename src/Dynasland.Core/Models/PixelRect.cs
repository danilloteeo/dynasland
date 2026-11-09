namespace Dynasland.Core.Models;

/// <summary>Retângulo em pixels físicos, em coordenadas do desktop virtual.</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}
