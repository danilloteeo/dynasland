namespace Dynasland.App.Controls;

/// <summary>Geometria da pílula do <see cref="SegmentedControl"/>: segmentos de largura igual dentro de uma folga.</summary>
public static class SegmentedLayout
{
    /// <summary>Posição esquerda e largura da pílula sob o segmento <paramref name="index"/>; largura 0 se não há seleção.</summary>
    public static (double Left, double Width) Pill(int count, int index, double totalWidth, double padding)
    {
        if (count <= 0 || index < 0 || index >= count)
        {
            return (padding, 0);
        }

        var segment = Math.Max(0, totalWidth - 2 * padding) / count;
        return (padding + index * segment, segment);
    }
}
