namespace Dynasland.Core.Models;

public enum DownloadState
{
    InProgress,
    Completed,
    Cancelled,
    Failed,
}

/// <summary>Um download informado por um <see cref="Interfaces.IDownloadProvider"/>.</summary>
/// <param name="Id">Único dentro do provedor; estável durante todo o download.</param>
/// <param name="FilePath">Caminho do arquivo final (quando concluído) ou temporário.</param>
/// <param name="TotalBytes">Tamanho total; <c>null</c> quando o provedor não sabe.</param>
/// <param name="BytesPerSecond">Velocidade atual; <c>null</c> até haver medições suficientes.</param>
public sealed record DownloadInfo(
    string Id,
    string ProviderName,
    string FileName,
    string? FilePath,
    long ReceivedBytes,
    long? TotalBytes,
    double? BytesPerSecond,
    DownloadState State,
    DateTimeOffset StartedAt)
{
    public double? Progress => TotalBytes is > 0 ? Math.Clamp((double)ReceivedBytes / TotalBytes.Value, 0, 1) : null;

    public TimeSpan? EstimatedRemaining =>
        TotalBytes is { } total && BytesPerSecond is > 0 && total > ReceivedBytes
            ? TimeSpan.FromSeconds((total - ReceivedBytes) / BytesPerSecond.Value)
            : null;
}
