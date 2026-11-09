using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.SystemMonitor;

/// <summary>Painel do sistema. Sensores ausentes aparecem como "—", nunca com valor estimado.</summary>
public sealed partial class SystemMonitorContent(Action<IslandState?> presentationChanged) : ObservableObject, IActivityContent
{
    public const string Unavailable = "—";
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [ObservableProperty]
    private string _cpuText = "…";

    [ObservableProperty]
    private double _cpuValue;

    [ObservableProperty]
    private string _cpuDetail = string.Empty;

    [ObservableProperty]
    private string _memoryText = "…";

    [ObservableProperty]
    private double _memoryValue;

    [ObservableProperty]
    private string _memoryDetail = string.Empty;

    [ObservableProperty]
    private string _gpuText = "…";

    [ObservableProperty]
    private double _gpuValue;

    [ObservableProperty]
    private string _gpuDetail = string.Empty;

    [ObservableProperty]
    private string _diskText = "…";

    [ObservableProperty]
    private double _diskValue;

    [ObservableProperty]
    private string _diskDetail = string.Empty;

    public string Glyph => Glyphs.Diagnostic;

    public void Apply(SystemMetrics metrics)
    {
        CpuText = Percent(metrics.CpuPercent) ?? (CpuText == "…" ? "…" : Unavailable);
        CpuValue = metrics.CpuPercent ?? 0;
        CpuDetail = metrics.CpuTemperatureCelsius is { } cpuTemp
            ? $"{cpuTemp.ToString("0", PtBr)} °C"
            : "Temperatura indisponível";

        MemoryText = Percent(metrics.Memory.Percent) ?? Unavailable;
        MemoryValue = metrics.Memory.Percent;
        MemoryDetail = $"{TextFormat.Bytes(metrics.Memory.UsedBytes)} de {TextFormat.Bytes(metrics.Memory.TotalBytes)}";

        GpuText = Percent(metrics.GpuPercent) ?? Unavailable;
        GpuValue = metrics.GpuPercent ?? 0;
        GpuDetail = metrics.GpuTemperatureCelsius is { } gpuTemp
            ? $"{gpuTemp.ToString("0", PtBr)} °C"
            : "Temperatura indisponível";

        var disk = metrics.Disks.FirstOrDefault(d => d.IsSystemDrive) ?? metrics.Disks.FirstOrDefault();
        if (disk is null)
        {
            DiskText = Unavailable;
            DiskValue = 0;
            DiskDetail = string.Empty;
        }
        else
        {
            var others = metrics.Disks.Count - 1;
            DiskText = TextFormat.Bytes(disk.FreeBytes);
            DiskValue = 100 - disk.FreePercent;
            DiskDetail = $"livres em {disk.Name.TrimEnd('\\')}" + (others > 0 ? $" · +{others}" : string.Empty);
        }
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state == IslandState.Interactive ? new IslandDimensions(420, 132, 34) : null;

    public void OnPresentationChanged(IslandState? state) => presentationChanged(state);

    private static string? Percent(double? value) => value is { } v ? $"{v.ToString("0", PtBr)}%" : null;
}

public sealed class SystemAlertContent(SystemAlert alert) : IActivityContent
{
    public string Title => alert.Title;

    public string Detail => alert.Detail;

    public bool IsSevere => alert.IsSevere;

    public string Glyph => Glyphs.Warning;

    public IslandDimensions? GetPreferredDimensions(IslandState state) =>
        state is IslandState.Expanded or IslandState.Interactive ? new IslandDimensions(360, 64, 30) : null;
}
