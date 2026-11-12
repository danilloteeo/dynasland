using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Dynasland.App.Controls;

/// <summary>
/// Mascote em pixel art: a própria ilha, uma cápsula com dois olhos. Desenhado em grade de blocos na cor
/// do texto herdada, então segue tema e contraste de onde estiver (marca do menu, estados vazios, novidades).
/// </summary>
public sealed class PixelMascot : FrameworkElement
{
    // 'X' = bloco cheio; '.' = vazio (os olhos são furos, o fundo aparece por eles).
    private static readonly string[] Grid =
    [
        "..XXXXXXXXXX..",
        ".XXXXXXXXXXXX.",
        "XXXX..XX..XXXX",
        "XXXX..XX..XXXX",
        "XXXXXXXXXXXXXX",
        ".XXXXXXXXXXXX.",
        "..XXXXXXXXXX..",
    ];

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(PixelMascot),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public PixelMascot()
    {
        Width = 56;
        Height = 28;
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var columns = Grid[0].Length;
        var cell = Math.Floor(Math.Min(ActualWidth / columns, ActualHeight / Grid.Length));
        if (cell < 1)
        {
            return;
        }

        var left = (ActualWidth - cell * columns) / 2;
        var top = (ActualHeight - cell * Grid.Length) / 2;
        for (var y = 0; y < Grid.Length; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                if (Grid[y][x] == 'X')
                {
                    dc.DrawRectangle(Foreground, null, new Rect(left + x * cell, top + y * cell, cell, cell));
                }
            }
        }
    }
}
