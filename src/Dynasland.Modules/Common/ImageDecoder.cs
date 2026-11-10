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
