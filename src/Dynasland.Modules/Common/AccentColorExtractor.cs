using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dynasland.Modules.Common;

/// <summary>Extrai a cor dominante de um ícone, para dar ao cartão a identidade do aplicativo.</summary>
public static class AccentColorExtractor
{
    private const int SampleSize = 32;
    private const int HueBuckets = 12;
    private const double MinSaturation = 0.25;
    private const double MinLightness = 0.15;
    private const double MaxLightness = 0.90;
    private const double MinColorfulShare = 0.05;

    /// <summary>Luminosidade mínima do resultado, para a cor ficar legível sobre o preto da ilha.</summary>
    private const double MinResultLightness = 0.55;

    /// <summary><c>null</c> se a imagem for inválida ou não tiver uma cor aproveitável (ícone cinza, por exemplo).</summary>
    public static Color? FromImage(ImageSource? image)
    {
        if (image is not BitmapSource source || source.PixelWidth == 0 || source.PixelHeight == 0)
        {
            return null;
        }

        try
        {
            var scale = Math.Min(1.0, (double)SampleSize / Math.Max(source.PixelWidth, source.PixelHeight));
            BitmapSource sample = scale < 1 ? new TransformedBitmap(source, new ScaleTransform(scale, scale)) : source;
            if (sample.Format != PixelFormats.Bgra32)
            {
                sample = new FormatConvertedBitmap(sample, PixelFormats.Bgra32, null, 0);
            }

            var pixels = new byte[sample.PixelWidth * sample.PixelHeight * 4];
            sample.CopyPixels(pixels, sample.PixelWidth * 4, 0);
            return FromPixels(pixels);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <param name="bgra">Pixels em BGRA, 4 bytes cada.</param>
    public static Color? FromPixels(ReadOnlySpan<byte> bgra)
    {
        Span<double> weight = stackalloc double[HueBuckets];
        Span<double> red = stackalloc double[HueBuckets];
        Span<double> green = stackalloc double[HueBuckets];
        Span<double> blue = stackalloc double[HueBuckets];
        Span<int> count = stackalloc int[HueBuckets];
        weight.Clear();
        red.Clear();
        green.Clear();
        blue.Clear();
        count.Clear();

        var opaque = 0;
        var colorful = 0;
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            if (bgra[i + 3] < 128)
            {
                continue;
            }

            opaque++;
            var (hue, saturation, lightness) = ToHsl(bgra[i + 2], bgra[i + 1], bgra[i]);
            if (saturation < MinSaturation || lightness < MinLightness || lightness > MaxLightness)
            {
                continue;
            }

            colorful++;
            var bucket = Math.Min(HueBuckets - 1, (int)(hue / 360 * HueBuckets));
            weight[bucket] += saturation;
            red[bucket] += bgra[i + 2];
            green[bucket] += bgra[i + 1];
            blue[bucket] += bgra[i];
            count[bucket]++;
        }

        if (opaque == 0 || colorful < opaque * MinColorfulShare)
        {
            return null;
        }

        var best = 0;
        for (var bucket = 1; bucket < HueBuckets; bucket++)
        {
            if (weight[bucket] > weight[best])
            {
                best = bucket;
            }
        }

        var (h, s, l) = ToHsl(red[best] / count[best], green[best] / count[best], blue[best] / count[best]);
        return FromHsl(h, s, Math.Max(l, MinResultLightness));
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl(double r, double g, double b)
    {
        r /= 255;
        g /= 255;
        b /= 255;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;
        var delta = max - min;
        if (delta < 1e-6)
        {
            return (0, 0, lightness);
        }

        var saturation = delta / (1 - Math.Abs((2 * lightness) - 1));
        double hue;
        if (max == r)
        {
            hue = ((g - b) / delta) % 6;
        }
        else if (max == g)
        {
            hue = ((b - r) / delta) + 2;
        }
        else
        {
            hue = ((r - g) / delta) + 4;
        }

        hue *= 60;
        return (hue < 0 ? hue + 360 : hue, Math.Min(1, saturation), lightness);
    }

    private static Color FromHsl(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var sector = hue / 60;
        var x = chroma * (1 - Math.Abs((sector % 2) - 1));
        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        var m = lightness - (chroma / 2);
        return Color.FromRgb(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
