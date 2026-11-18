using System.Windows.Media;
using DynaIsland.App.Services;
using DynaIsland.Core.Models;
using DynaIsland.Core.Services;

namespace DynaIsland.Tests.Core;

public class ThemePaletteTests
{
    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static double Luminance(Color c) => AccentColors.RelativeLuminance(c.R, c.G, c.B);

    private static double Contrast(Color a, Color b)
    {
        var (hi, lo) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (hi + 0.05) / (lo + 0.05);
    }

    [Fact]
    public void Text_is_readable_on_the_solid_fallback_surfaces()
    {
        foreach (var surface in new[] { "SettingsBackgroundBrush", "SettingsSidebarBrush", "SettingsCardBrush" })
        {
            Assert.True(Contrast(Hex(ThemeService.Palette["SettingsTextBrush"]), Hex(ThemeService.Palette[surface])) >= 7, $"{surface}: texto principal");
            Assert.True(Contrast(Hex(ThemeService.Palette["SettingsSecondaryTextBrush"]), Hex(ThemeService.Palette[surface])) >= 4.5, $"{surface}: texto secundário");
        }
    }

    [Fact]
    public void Dark_text_is_readable_on_every_preset_accent()
    {
        foreach (var (name, hex) in AccentColors.Presets)
        {
            Assert.True(Contrast(ThemeService.OnAccent, Hex(hex)) >= 7, name);
        }
    }

    [Theory]
    [InlineData(0.70, 179)]
    [InlineData(1.0, 255)]
    [InlineData(0.2, 128)]
    public void Glass_uses_the_chosen_opacity_clamped_to_half(double opacity, byte alpha)
    {
        var glass = ThemeService.Glass(opacity);

        Assert.Equal(alpha, glass.A);
        Assert.Equal((byte)0x10, glass.R);
        Assert.Equal((byte)0x12, glass.G);
        Assert.Equal((byte)0x1A, glass.B);
    }

    [Fact]
    public void Chosen_accent_is_used_when_not_following_windows()
    {
        var settings = IslandSettings.Default with { AccentColor = "#FFC27A", AccentFollowsWindows = false };

        Assert.Equal("#FFC27A", ThemeService.EffectiveAccent(settings));
    }

    [Fact]
    public void Default_accent_is_the_first_preset()
    {
        Assert.Equal(AccentColors.Default, new IslandSettings().AccentColor);
    }
}
