using Windows.Storage.Streams;

namespace Dynasland.Infrastructure.Windows;

internal static class WinRtStreams
{
    private const ulong MaxImageBytes = 4 * 1024 * 1024;

    /// <summary>Lê uma imagem (capa, ícone) para memória; <c>null</c> se ausente, vazia ou grande demais.</summary>
    public static async Task<byte[]?> ReadAllBytesAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        using var stream = await reference.OpenReadAsync();
        if (stream.Size == 0 || stream.Size > MaxImageBytes)
        {
            return null;
        }

        using var reader = new DataReader(stream);
        var length = (uint)stream.Size;
        await reader.LoadAsync(length);
        var bytes = new byte[length];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
