using System.Windows;
using System.Windows.Media;

namespace DynaIsland.App.Views;

/// <summary>
/// Contorno da cápsula (flutuante: quatro cantos arredondados; encaixada: só os de baixo) e o recorte da sombra,
/// que deixa um buraco no lugar da cápsula para a sombra não escurecer o vidro translúcido.
/// </summary>
public static class IslandShapeGeometry
{
    /// <summary>Folga em volta da cápsula onde a sombra desfocada ainda aparece.</summary>
    private const double ShadowBleed = 80;

    public static Geometry Create(double width, double height, double radius, bool notch)
    {
        width = Math.Max(0, width);
        height = Math.Max(0, height);
        radius = Math.Clamp(radius, 0, Math.Min(width, height) / 2);
        if (!notch)
        {
            var rounded = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
            rounded.Freeze();
            return rounded;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
            context.LineTo(new Point(width, 0), isStroked: true, isSmoothJoin: false);
            context.LineTo(new Point(width, height - radius), true, false);
            context.ArcTo(new Point(width - radius, height), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            context.LineTo(new Point(radius, height), true, false);
            context.ArcTo(new Point(0, height - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    public static Geometry ShadowClip(double width, double height, double radius, bool notch)
    {
        var area = new RectangleGeometry(new Rect(-ShadowBleed, -ShadowBleed, width + ShadowBleed * 2, height + ShadowBleed * 2));
        var clip = new CombinedGeometry(GeometryCombineMode.Exclude, area, Create(width, height, radius, notch));
        clip.Freeze();
        return clip;
    }
}
