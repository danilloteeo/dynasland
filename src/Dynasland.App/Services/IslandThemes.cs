using System.Windows;
using System.Windows.Media;
using Dynasland.Core.Models;

namespace Dynasland.App.Services;

/// <summary>Temas (fundos) da ilha: nome e as duas cores do degradê vertical.</summary>
public static class IslandThemes
{
    public static readonly IReadOnlyList<(IslandTheme Theme, string Name)> All =
    [
        (IslandTheme.Black, "Preto"),
        (IslandTheme.Graphite, "Grafite"),
        (IslandTheme.Midnight, "Meia-noite"),
        (IslandTheme.Aurora, "Aurora"),
        (IslandTheme.Sunset, "Pôr do sol"),
        (IslandTheme.Forest, "Floresta"),
        (IslandTheme.Steel, "Aço"),
        (IslandTheme.Wine, "Vinho"),
        (IslandTheme.Copper, "Cobre"),
        (IslandTheme.Ember, "Brasa"),
        (IslandTheme.Magenta, "Magenta"),
        (IslandTheme.Dusk, "Crepúsculo"),
        (IslandTheme.Accent, "Cor de destaque"),
    ];

    /// <summary>
    /// Cores do início e do fim do degradê (topo e base; esquerda e direita no <see cref="IslandTheme.Dusk"/>).
    /// Sempre escuras, para o texto claro continuar legível.
    /// </summary>
    public static (Color Top, Color Bottom) Colors(IslandTheme theme, Color accent) => theme switch
    {
        IslandTheme.Steel => (Rgb(0x28, 0x34, 0x3F), Rgb(0x10, 0x16, 0x1C)),
        IslandTheme.Wine => (Rgb(0x4C, 0x10, 0x20), Rgb(0x1D, 0x06, 0x0D)),
        IslandTheme.Copper => (Rgb(0x4C, 0x2B, 0x13), Rgb(0x1B, 0x0F, 0x07)),
        IslandTheme.Ember => (Rgb(0x5C, 0x1C, 0x0B), Rgb(0x1F, 0x0B, 0x06)),
        IslandTheme.Magenta => (Rgb(0x4D, 0x17, 0x42), Rgb(0x1B, 0x0A, 0x23)),
        IslandTheme.Dusk => (Rgb(0x1B, 0x2B, 0x5E), Rgb(0x4C, 0x1B, 0x4C)),
        IslandTheme.Graphite => (Rgb(0x2C, 0x2C, 0x30), Rgb(0x17, 0x17, 0x1A)),
        IslandTheme.Midnight => (Rgb(0x13, 0x20, 0x3D), Rgb(0x07, 0x0C, 0x1A)),
        IslandTheme.Aurora => (Rgb(0x2E, 0x14, 0x5C), Rgb(0x0A, 0x24, 0x40)),
        IslandTheme.Sunset => (Rgb(0x4D, 0x16, 0x32), Rgb(0x33, 0x1A, 0x0B)),
        IslandTheme.Forest => (Rgb(0x11, 0x33, 0x26), Rgb(0x06, 0x15, 0x0E)),
        IslandTheme.Accent => (Shade(accent, 0.30), Shade(accent, 0.12)),
        _ => (System.Windows.Media.Colors.Black, System.Windows.Media.Colors.Black),
    };

    /// <summary>Fundo a partir de uma cor qualquer (ex.: a capa da música): o mesmo tom escuro do tema "Cor de destaque".</summary>
    public static (Color Top, Color Bottom) Tinted(Color color) => (Shade(color, 0.34), Shade(color, 0.11));

    /// <summary>O degradê do tema corre da esquerda para a direita (os demais, de cima para baixo).</summary>
    public static bool IsHorizontal(IslandTheme theme) => theme == IslandTheme.Dusk;

    /// <summary>Pincel do fundo da ilha com a opacidade escolhida em Aparência.</summary>
    public static Brush Background(IslandTheme theme, Color accent, double opacity)
    {
        var alpha = (byte)Math.Round(255 * Math.Clamp(opacity, 0, 1));
        var (top, bottom) = Colors(theme, accent);
        top.A = alpha;
        bottom.A = alpha;

        Brush brush = top == bottom
            ? new SolidColorBrush(top)
            : new LinearGradientBrush(top, bottom, new Point(0, 0), IsHorizontal(theme) ? new Point(1, 0) : new Point(0, 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>Mistura a cor com preto, mantendo <paramref name="amount"/> dela (0 a 1).</summary>
    private static Color Shade(Color color, double amount) =>
        Rgb((byte)Math.Round(color.R * amount), (byte)Math.Round(color.G * amount), (byte)Math.Round(color.B * amount));

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}
