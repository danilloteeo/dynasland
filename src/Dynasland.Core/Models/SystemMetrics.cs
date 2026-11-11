namespace Dynasland.Core.Models;

public sealed record MemoryUsage(ulong UsedBytes, ulong TotalBytes)
{
    public double Percent => TotalBytes == 0 ? 0 : UsedBytes * 100.0 / TotalBytes;
}

/// <param name="Name">Raiz da unidade (ex.: <c>C:\</c>).</param>
public sealed record DiskUsage(string Name, string Label, ulong TotalBytes, ulong FreeBytes, bool IsSystemDrive)
{
    public double FreePercent => TotalBytes == 0 ? 0 : FreeBytes * 100.0 / TotalBytes;
}

/// <summary>Tráfego de rede somado de todas as conexões ativas, em bytes por segundo.</summary>
public sealed record NetworkRate(double DownloadBytesPerSecond, double UploadBytesPerSecond);

/// <summary>Leitura do sistema. Valores <c>null</c> significam sensor indisponível — nunca estimados.</summary>
/// <param name="CpuTemperatureSource">De onde veio a temperatura da CPU (ex.: zona térmica ACPI).</param>
public sealed record SystemMetrics(
    double? CpuPercent,
    MemoryUsage Memory,
    double? GpuPercent,
    double? CpuTemperatureCelsius,
    string? CpuTemperatureSource,
    double? GpuTemperatureCelsius,
    IReadOnlyList<DiskUsage> Disks)
{
    /// <summary>Memória dedicada da GPU em uso; <c>null</c> se o Windows não informar.</summary>
    public ulong? VideoMemoryUsedBytes { get; init; }

    /// <summary>Memória dedicada total da GPU principal.</summary>
    public ulong? VideoMemoryTotalBytes { get; init; }

    /// <summary>Frequência atual da CPU (como no Gerenciador de Tarefas).</summary>
    public double? CpuFrequencyMhz { get; init; }

    /// <summary>Velocidade de rede desde a leitura anterior; <c>null</c> na primeira leitura ou sem rede.</summary>
    public NetworkRate? Network { get; init; }
}
