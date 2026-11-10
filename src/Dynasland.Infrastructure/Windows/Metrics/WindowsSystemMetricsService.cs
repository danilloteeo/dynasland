using System.Runtime.InteropServices;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;

namespace Dynasland.Infrastructure.Windows.Metrics;

/// <summary>
/// Métricas do sistema a partir de fontes que não exigem privilégios de administrador:
/// CPU por GetSystemTimes, memória por GlobalMemoryStatusEx, GPU pelos contadores
/// "GPU Engine" (mesma fonte do Gerenciador de Tarefas), temperatura da CPU pela zona térmica
/// ACPI e temperatura da GPU por NVML. Sensores ausentes resultam em <c>null</c>.
/// </summary>
public sealed class WindowsSystemMetricsService : ISystemMetricsService, IDisposable
{
    private const double KelvinOffset = 273.15;

    private readonly Lock _lock = new();
    private readonly Lazy<PdhWildcardCounter?> _gpuEngines = new(() => PdhWildcardCounter.TryCreate(@"\GPU Engine(*)\Utilization Percentage"));
    private readonly Lazy<PdhWildcardCounter?> _thermalZones = new(() => PdhWildcardCounter.TryCreate(@"\Thermal Zone Information(*)\Temperature"));
    private readonly Lazy<NvmlGpuSensor?> _nvml = new(NvmlGpuSensor.TryCreate);
    private readonly Lazy<PdhWildcardCounter?> _gpuMemory = new(() => PdhWildcardCounter.TryCreate(@"\GPU Adapter Memory(*)\Dedicated Usage"));
    private readonly Lazy<PdhWildcardCounter?> _cpuPerformance = new(() => PdhWildcardCounter.TryCreate(@"\Processor Information(_Total)\% Processor Performance"));
    private readonly Lazy<ulong?> _videoMemoryTotal = new(ReadVideoMemoryTotal);
    private readonly Lazy<double?> _baseFrequency = new(ReadBaseFrequencyMhz);

    private ulong _lastIdle;
    private ulong _lastTotal;

    public SystemMetrics Sample()
    {
        lock (_lock)
        {
            var nvml = _nvml.Value;
            return new SystemMetrics(
                ReadCpuPercent(),
                GetMemory(),
                ReadGpuPercent() ?? nvml?.ReadUtilization(),
                ReadThermalZoneCelsius(),
                _thermalZones.Value is null ? null : "Zona térmica (ACPI)",
                nvml?.ReadTemperature(),
                GetDisks())
            {
                VideoMemoryUsedBytes = ReadVideoMemoryUsed(),
                VideoMemoryTotalBytes = _videoMemoryTotal.Value,
                CpuFrequencyMhz = ReadCpuFrequency(),
            };
        }
    }

    public MemoryUsage GetMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status)
            ? new MemoryUsage(status.TotalPhys - status.AvailPhys, status.TotalPhys)
            : new MemoryUsage(0, 0);
    }

    public IReadOnlyList<DiskUsage> GetDisks()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
        var disks = new List<DiskUsage>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive is { DriveType: DriveType.Fixed, IsReady: true })
                {
                    disks.Add(new DiskUsage(
                        drive.Name,
                        drive.VolumeLabel,
                        (ulong)drive.TotalSize,
                        (ulong)drive.AvailableFreeSpace,
                        string.Equals(drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase)));
                }
            }
            catch (IOException)
            {
                // Unidade removida durante a leitura.
            }
        }

        return disks;
    }

    /// <summary>Memória dedicada em uso, do adaptador mais usado (soma por adaptador).</summary>
    private ulong? ReadVideoMemoryUsed()
    {
        var samples = _gpuMemory.Value?.Read();
        return samples is { Count: > 0 } ? (ulong)samples.Max(s => s.Value) : null;
    }

    /// <summary>"% Processor Performance" × frequência base: a frequência atual com turbo, como no Gerenciador de Tarefas.</summary>
    private double? ReadCpuFrequency()
    {
        var performance = _cpuPerformance.Value?.Read();
        return performance is { Count: > 0 } && _baseFrequency.Value is { } baseMhz
            ? Math.Round(baseMhz * performance[0].Value / 100)
            : null;
    }

    private static double? ReadBaseFrequencyMhz()
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return key?.GetValue("~MHz") is int mhz && mhz > 0 ? mhz : null;
    }

    /// <summary>Memória dedicada informada pelo driver de vídeo (chave da classe de adaptadores de vídeo).</summary>
    private static ulong? ReadVideoMemoryTotal()
    {
        const string displayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(displayClass);
        if (root is null)
        {
            return null;
        }

        ulong? largest = null;
        foreach (var name in root.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)))
        {
            try
            {
                using var adapter = root.OpenSubKey(name);
                var value = adapter?.GetValue("HardwareInformation.qwMemorySize") switch
                {
                    long q => (ulong)q,
                    byte[] { Length: >= 8 } b => BitConverter.ToUInt64(b, 0),
                    _ => adapter?.GetValue("HardwareInformation.MemorySize") switch
                    {
                        int d => (ulong)(uint)d,
                        byte[] { Length: >= 4 } b => BitConverter.ToUInt32(b, 0),
                        _ => 0UL,
                    },
                };
                if (value > (largest ?? 0))
                {
                    largest = value;
                }
            }
            catch (System.Security.SecurityException)
            {
                // Chave de outro adaptador sem permissão de leitura.
            }
        }

        return largest;
    }

    /// <summary>Uso desde a leitura anterior; <c>null</c> na primeira chamada.</summary>
    private double? ReadCpuPercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return null;
        }

        var idleTicks = ToUInt64(idle);
        var totalTicks = ToUInt64(kernel) + ToUInt64(user); // o tempo de kernel inclui o ocioso
        var hadPrevious = _lastTotal != 0;
        var idleDelta = idleTicks - _lastIdle;
        var totalDelta = totalTicks - _lastTotal;
        _lastIdle = idleTicks;
        _lastTotal = totalTicks;

        if (!hadPrevious || totalDelta == 0)
        {
            return null;
        }

        return Math.Clamp(100.0 * (totalDelta - idleDelta) / totalDelta, 0, 100);
    }

    /// <summary>
    /// Como o Gerenciador de Tarefas: soma o uso de cada motor da GPU entre todos os processos
    /// e reporta o motor mais ocupado.
    /// </summary>
    private double? ReadGpuPercent()
    {
        if (_gpuEngines.Value is not { } counter)
        {
            return null;
        }

        var samples = counter.Read();
        if (samples.Count == 0)
        {
            return null;
        }

        var busiestEngine = samples
            .GroupBy(s => EngineKey(s.Instance))
            .Select(g => g.Sum(s => s.Value))
            .DefaultIfEmpty(0)
            .Max();

        return Math.Clamp(busiestEngine, 0, 100);
    }

    /// <summary><c>pid_10_luid_0x0_0x1_phys_0_eng_3_engtype_3D</c> → <c>0x0_0x1_phys_0_eng_3</c>.</summary>
    internal static string EngineKey(string instance)
    {
        var start = instance.IndexOf("luid_", StringComparison.Ordinal);
        var end = instance.IndexOf("_engtype", StringComparison.Ordinal);
        return start >= 0 && end > start ? instance[(start + 5)..end] : instance;
    }

    private double? ReadThermalZoneCelsius()
    {
        if (_thermalZones.Value is not { } counter)
        {
            return null;
        }

        var kelvin = counter.Read().Select(s => s.Value).DefaultIfEmpty(0).Max();
        var celsius = kelvin - KelvinOffset;
        return celsius is > 0 and < 125 ? Math.Round(celsius, 1) : null;
    }

    public void Dispose()
    {
        if (_gpuEngines.IsValueCreated)
        {
            _gpuEngines.Value?.Dispose();
        }

        if (_thermalZones.IsValueCreated)
        {
            _thermalZones.Value?.Dispose();
        }

        if (_nvml.IsValueCreated)
        {
            _nvml.Value?.Dispose();
        }
    }

    private static ulong ToUInt64(FileTime time) => ((ulong)time.High << 32) | time.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
