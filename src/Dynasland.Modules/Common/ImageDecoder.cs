using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dynasland.Modules.Common;

public static class ImageDecoder
{
    /// <summary>
    /// Decodifica uma imagem (capa, ícone) já reduzida para <paramref name="decodeWidth"/> pixels.
    /// O resultado é congelado, então pode ser criado em qualquer thread.
    /// </summary>
    public static ImageSource? Decode(byte[]? bytes, int decodeWidth)
    {
        if (bytes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodeWidth;
            image.StreamSource = new MemoryStream(bytes, writable: false);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            // Formato não suportado ou dados corrompidos: a interface mostra o ícone padrão.
            return null;
        }
    }

    /// <summary>
    /// Recorta a margem transparente em volta do desenho. Ícones de aplicativos empacotados costumam vir
    /// pequenos no centro de uma tela maior; sem o recorte, aparecem minúsculos.
    /// </summary>
    public static ImageSource? TrimTransparentMargin(ImageSource? image)
    {
        if (image is not BitmapSource source || source.PixelWidth == 0 || source.PixelHeight == 0)
        {
            return image;
        }

        try
        {
            BitmapSource bgra = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = bgra.PixelWidth, height = bgra.PixelHeight;
            var pixels = new byte[width * height * 4];
            bgra.CopyPixels(pixels, width * 4, 0);

            int left = width, top = height, right = -1, bottom = -1;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (pixels[(((y * width) + x) * 4) + 3] > TrimAlphaThreshold)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }
            }

            if (right < 0 || (left == 0 && top == 0 && right == width - 1 && bottom == height - 1))
            {
                return image;
            }

            var cropped = new CroppedBitmap(bgra, new System.Windows.Int32Rect(left, top, right - left + 1, bottom - top + 1));
            cropped.Freeze();
            return cropped;
        }
        catch (Exception)
        {
            return image;
        }
    }

    private const byte TrimAlphaThreshold = 16;

    /// <summary>Miniatura em PNG (para guardar no histórico); <c>null</c> se a imagem for inválida.</summary>
    public static byte[]? EncodeThumbnailPng(byte[]? bytes, int width)
    {
        if (Decode(bytes, width) is not BitmapSource bitmap)
        {
            return null;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
