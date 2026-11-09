using System.Globalization;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.SystemMonitor;

/// <param name="Key">Identifica o alerta (um por tipo/unidade), para não repetir.</param>
public sealed record SystemAlert(string Key, string Title, string Detail, bool IsSevere);

/// <summary>
/// Decide quando avisar sobre memória ou disco. Memória exige duas leituras seguidas acima do limite
/// (picos passageiros não contam) e respeita um intervalo mínimo; cada disco avisa uma vez e só volta
/// a avisar depois de liberar espaço (histerese).
/// </summary>
public sealed class SystemAlertPolicy
{
    public const double MemoryThresholdPercent = 90;
    public const double DiskFreeThresholdPercent = 10;
    public const double DiskRearmPercent = 15;
    public static readonly TimeSpan MemoryCooldown = TimeSpan.FromMinutes(15);

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly HashSet<string> _alertedDisks = new(StringComparer.OrdinalIgnoreCase);
    private int _consecutiveHighMemory;
    private DateTimeOffset _lastMemoryAlert = DateTimeOffset.MinValue;

    public IReadOnlyList<SystemAlert> Evaluate(
        MemoryUsage memory,
        IReadOnlyList<DiskUsage> disks,
        DateTimeOffset now,
        double memoryThreshold = MemoryThresholdPercent,
        double diskFreeThreshold = DiskFreeThresholdPercent)
    {
        var alerts = new List<SystemAlert>();

        if (memory.TotalBytes > 0 && memory.Percent >= memoryThreshold)
        {
            _consecutiveHighMemory++;
            if (_consecutiveHighMemory >= 2 && now - _lastMemoryAlert >= MemoryCooldown)
            {
                _lastMemoryAlert = now;
                alerts.Add(new SystemAlert(
                    "memory",
                    "Memória quase cheia",
                    $"{memory.Percent.ToString("0", PtBr)}% em uso · {TextFormat.Bytes(memory.TotalBytes - memory.UsedBytes)} livres",
                    IsSevere: memory.Percent >= 97));
            }
        }
        else
        {
            _consecutiveHighMemory = 0;
        }

        foreach (var disk in disks)
        {
            if (disk.TotalBytes == 0)
            {
                continue;
            }

            if (disk.FreePercent < diskFreeThreshold && _alertedDisks.Add(disk.Name))
            {
                alerts.Add(new SystemAlert(
                    $"disk.{disk.Name}",
                    $"Pouco espaço em {disk.Name.TrimEnd('\\')}",
                    $"{TextFormat.Bytes(disk.FreeBytes)} livres de {TextFormat.Bytes(disk.TotalBytes)}",
                    IsSevere: disk.FreePercent < 3));
            }
            else if (disk.FreePercent >= diskFreeThreshold + (DiskRearmPercent - DiskFreeThresholdPercent))
            {
                _alertedDisks.Remove(disk.Name);
            }
        }

        return alerts;
    }
}
