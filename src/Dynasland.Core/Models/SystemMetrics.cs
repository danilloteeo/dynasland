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

/// <summary>Leitura do sistema. Valores <c>null</c> significam sensor indisponível — nunca estimados.</summary>
/// <param name="CpuTemperatureSource">De onde veio a temperatura da CPU (ex.: zona térmica ACPI).</param>
public sealed record SystemMetrics(
    double? CpuPercent,
    MemoryUsage Memory,
    double? GpuPercent,
    double? CpuTemperatureCelsius,
    string? CpuTemperatureSource,
    double? GpuTemperatureCelsius,
    IReadOnlyList<DiskUsage> Disks);
