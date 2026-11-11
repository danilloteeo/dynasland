using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dynasland.App.Views;

/// <summary>Fotografia de um elemento da ilha, usada como "fantasma" no cross-fade da troca de conteúdo.</summary>
public static class IslandSnapshotImage
{
    /// <summary>
    /// Renderiza o elemento em um bitmap congelado na resolução real do monitor (DPI do elemento).
    /// O VisualBrush desenha só o conteúdo, sem a posição do elemento no pai.
    /// </summary>
    public static BitmapSource Capture(Visual element, double width, double height)
    {
        var dpi = VisualTreeHelper.GetDpi(element);
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(width * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(height * dpi.DpiScaleY));

        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            var brush = new VisualBrush(element)
            {
                // Recorte pelo tamanho do layout, não pelos limites do que foi desenhado.
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, width, height),
                Stretch = Stretch.Fill,
            };
            context.DrawRectangle(brush, null, new Rect(0, 0, width, height));
        }

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        bitmap.Freeze();
        return bitmap;
    }
}
