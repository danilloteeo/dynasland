using System.Windows.Media;
using Dynasland.App.Services;
using Dynasland.Core.Models;

namespace Dynasland.Tests.Core;

public class ThemePaletteTests
{
    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static double Contrast(Color a, Color b)
    {
        var (hi, lo) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (hi + 0.05) / (lo + 0.05);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Text_is_readable_on_background_and_cards(bool dark)
    {
        Color Pick(string key) => Hex(dark ? ThemeService.Palette[key].Dark : ThemeService.Palette[key].Light);

        foreach (var surface in new[] { "SettingsBackgroundBrush", "SettingsSidebarBrush", "SettingsCardBrush" })
        {
            Assert.True(Contrast(Pick("SettingsTextBrush"), Pick(surface)) >= 7, $"{surface}: texto principal");
            Assert.True(Contrast(Pick("SettingsSecondaryTextBrush"), Pick(surface)) >= 4.5, $"{surface}: texto secundário");
        }
    }

    [Fact]
    public void Dark_palette_uses_the_volum_identity()
    {
        Assert.Equal("#090909", ThemeService.Palette["SettingsBackgroundBrush"].Dark);
        Assert.Equal("#101010", ThemeService.Palette["SettingsCardBrush"].Dark);
        Assert.Equal("#1C1C1C", ThemeService.Palette["SettingsBorderBrush"].Dark);
    }

    [Fact]
    public void White_accent_becomes_near_black_on_the_light_theme_only()
    {
        Assert.Equal(Hex("#FFFFFF"), ThemeService.AccentFor(Hex("#FFFFFF"), dark: true));
        Assert.Equal(Hex("#111111"), ThemeService.AccentFor(Hex("#FFFFFF"), dark: false));
        Assert.Equal(Hex("#0A84FF"), ThemeService.AccentFor(Hex("#0A84FF"), dark: false));
    }

    [Fact]
    public void Default_accent_is_white_and_listed_as_a_preset()
    {
        Assert.Equal("#FFFFFF", new IslandSettings().AccentColor);
        Assert.Contains(Dynasland.App.ViewModels.SettingsViewModel.AccentPresets, p => p.Hex == "#FFFFFF");
    }
}
