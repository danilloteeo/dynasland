using System.Windows.Media;
using Dynasland.App.Services;
using Dynasland.Core.Models;

namespace Dynasland.Tests.Core;

public class IslandThemesTests
{
    private static readonly Color Accent = Color.FromRgb(0x30, 0xD1, 0x58);

    [Fact]
    public void Every_theme_is_listed_and_dark_enough_for_light_text()
    {
        Assert.Equal(Enum.GetValues<IslandTheme>().Order(), IslandThemes.All.Select(t => t.Theme).Order());

        foreach (var theme in Enum.GetValues<IslandTheme>())
        {
            var (top, bottom) = IslandThemes.Colors(theme, Color.FromRgb(0xFF, 0xD6, 0x0A));
            Assert.True(Luminance(top) < 0.3, $"{theme}: topo claro demais");
            Assert.True(Luminance(bottom) < 0.3, $"{theme}: base clara demais");
        }
    }

    [Fact]
    public void Black_theme_is_a_solid_brush_with_the_chosen_opacity()
    {
        var brush = Assert.IsType<SolidColorBrush>(IslandThemes.Background(IslandTheme.Black, Accent, 0.5));

        Assert.Equal(Color.FromArgb(128, 0, 0, 0), brush.Color);
    }

    [Fact]
    public void Accent_theme_follows_the_accent_color()
    {
        var brush = Assert.IsType<LinearGradientBrush>(IslandThemes.Background(IslandTheme.Accent, Accent, 1));

        var top = brush.GradientStops[0].Color;
        Assert.True(top.G > top.R && top.G > top.B);
        Assert.Equal(255, top.A);
    }

    private static double Luminance(Color color) => (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
}
