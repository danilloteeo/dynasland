using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dynasland.Modules.Common;

namespace Dynasland.Tests.Modules;

public class AccentColorExtractorTests
{
    private static readonly Color Green = Color.FromRgb(0x25, 0xD3, 0x66);

    [Fact]
    public void Single_color_icon_yields_that_hue()
    {
        var accent = AccentColorExtractor.FromPixels(Pixels((Green, 255, 64)));

        Assert.NotNull(accent);
        Assert.True(accent.Value.G > accent.Value.R && accent.Value.G > accent.Value.B);
    }

    [Fact]
    public void White_background_is_ignored_in_favor_of_the_colored_mark()
    {
        var accent = AccentColorExtractor.FromPixels(Pixels((Colors.White, 255, 80), (Color.FromRgb(0x58, 0x65, 0xF2), 255, 20)));

        Assert.NotNull(accent);
        Assert.True(accent.Value.B > accent.Value.R && accent.Value.B > accent.Value.G);
    }

    [Fact]
    public void Most_saturated_family_wins_over_a_duller_one()
    {
        var accent = AccentColorExtractor.FromPixels(Pixels((Color.FromRgb(0xE0, 0x20, 0x20), 255, 40), (Color.FromRgb(0x70, 0x80, 0xA0), 255, 40)));

        Assert.NotNull(accent);
        Assert.True(accent.Value.R > accent.Value.B);
    }

    [Fact]
    public void Gray_icon_has_no_accent()
    {
        Assert.Null(AccentColorExtractor.FromPixels(Pixels((Color.FromRgb(0x80, 0x80, 0x80), 255, 50), (Colors.White, 255, 50))));
    }

    [Fact]
    public void Transparent_or_empty_icon_has_no_accent()
    {
        Assert.Null(AccentColorExtractor.FromPixels(Pixels((Green, 0, 64))));
        Assert.Null(AccentColorExtractor.FromPixels([]));
        Assert.Null(AccentColorExtractor.FromImage(null));
    }

    [Fact]
    public void Dark_color_is_lightened_to_stay_readable_on_black()
    {
        var accent = AccentColorExtractor.FromPixels(Pixels((Color.FromRgb(0x10, 0x20, 0x70), 255, 64)));

        Assert.NotNull(accent);
        var lightness = (Math.Max(accent.Value.R, Math.Max(accent.Value.G, accent.Value.B)) + Math.Min(accent.Value.R, Math.Min(accent.Value.G, accent.Value.B))) / 510.0;
        Assert.True(lightness >= 0.54, $"luminosidade {lightness}");
        Assert.True(accent.Value.B > accent.Value.R);
    }

    [Fact]
    public void Reads_the_color_from_a_bitmap()
    {
        var pixels = Pixels((Green, 255, 48 * 48));
        var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgra32, null, pixels, 48 * 4);
        bitmap.Freeze();

        var accent = AccentColorExtractor.FromImage(bitmap);

        Assert.NotNull(accent);
        Assert.True(accent.Value.G > accent.Value.R && accent.Value.G > accent.Value.B);
    }

    private static byte[] Pixels(params (Color Color, byte Alpha, int Count)[] runs)
    {
        var pixels = new List<byte>();
        foreach (var (color, alpha, count) in runs)
        {
            for (var i = 0; i < count; i++)
            {
                pixels.AddRange([color.B, color.G, color.R, alpha]);
            }
        }

        return [.. pixels];
    }
}
