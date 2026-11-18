using DynaIsland.Core.Services;

namespace DynaIsland.Tests.Core;

public class AccentColorsTests
{
    private static (byte R, byte G, byte B) Rgb(string hex) =>
        (Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16));

    [Fact]
    public void Default_is_ice_blue_and_listed_first()
    {
        Assert.Equal("#9CC2FF", AccentColors.Default);
        Assert.Equal(AccentColors.Default, AccentColors.Presets[0].Hex);
        Assert.Equal(6, AccentColors.Presets.Count);
    }

    [Fact]
    public void Every_preset_is_light_enough_for_dark_text()
    {
        foreach (var (name, hex) in AccentColors.Presets)
        {
            var (r, g, b) = Rgb(hex);
            Assert.True(AccentColors.RelativeLuminance(r, g, b) >= AccentColors.MinLuminance, name);
        }
    }

    [Fact]
    public void Dark_windows_accent_is_lightened_until_readable()
    {
        // Azul padrão do Windows (#0078D4): escuro demais para texto escuro em cima.
        var hex = AccentColors.EnsureReadable(0x00, 0x78, 0xD4);
        var (r, g, b) = Rgb(hex);

        Assert.Matches("^#[0-9A-F]{6}$", hex);
        Assert.True(AccentColors.RelativeLuminance(r, g, b) >= AccentColors.MinLuminance);
        Assert.NotEqual("#FFFFFF", hex);
        Assert.True(b > r, "continua azulado");
    }

    [Fact]
    public void Light_color_is_kept_as_is()
    {
        Assert.Equal("#9CC2FF", AccentColors.EnsureReadable(0x9C, 0xC2, 0xFF));
    }
}
