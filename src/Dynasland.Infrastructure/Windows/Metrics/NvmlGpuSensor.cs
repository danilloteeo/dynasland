using System.Runtime.InteropServices;

namespace Dynasland.Infrastructure.Windows.Metrics;

/// <summary>
/// Temperatura e uso de GPUs NVIDIA via NVML (nvml.dll, instalada com o driver em placas suportadas).
/// Carregada dinamicamente: sem NVML, o sensor simplesmente não existe.
/// </summary>
internal sealed class NvmlGpuSensor : IDisposable
{
    private const int NvmlSuccess = 0;
    private const int NvmlTemperatureGpu = 0;

    private readonly IntPtr _library;
    private readonly IntPtr _device;
    private readonly NvmlShutdown _shutdown;
    private readonly NvmlDeviceGetTemperature _getTemperature;
    private readonly NvmlDeviceGetUtilizationRates _getUtilization;

    private NvmlGpuSensor(IntPtr library, IntPtr device, NvmlShutdown shutdown, NvmlDeviceGetTemperature temperature, NvmlDeviceGetUtilizationRates utilization)
    {
        _library = library;
        _device = device;
        _shutdown = shutdown;
        _getTemperature = temperature;
        _getUtilization = utilization;
    }

    public static NvmlGpuSensor? TryCreate()
    {
        var candidates = new[]
        {
            "nvml.dll",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvml.dll"),
        };

        foreach (var candidate in candidates)
        {
            if (!NativeLibrary.TryLoad(candidate, out var library))
            {
                continue;
            }

            try
            {
                var init = Export<NvmlInit>(library, "nvmlInit_v2");
                var getHandle = Export<NvmlDeviceGetHandleByIndex>(library, "nvmlDeviceGetHandleByIndex_v2");
                if (init() == NvmlSuccess && getHandle(0, out var device) == NvmlSuccess)
                {
                    return new NvmlGpuSensor(
                        library,
                        device,
                        Export<NvmlShutdown>(library, "nvmlShutdown"),
                        Export<NvmlDeviceGetTemperature>(library, "nvmlDeviceGetTemperature"),
                        Export<NvmlDeviceGetUtilizationRates>(library, "nvmlDeviceGetUtilizationRates"));
                }
            }
            catch (EntryPointNotFoundException)
            {
                // Versão de NVML incompatível.
            }

            NativeLibrary.Free(library);
        }

        return null;
    }

    public double? ReadTemperature() =>
        _getTemperature(_device, NvmlTemperatureGpu, out var celsius) == NvmlSuccess ? celsius : null;

    public double? ReadUtilization() =>
        _getUtilization(_device, out var utilization) == NvmlSuccess ? utilization.Gpu : null;

    public void Dispose()
    {
        _shutdown();
        NativeLibrary.Free(_library);
    }

    private static T Export<T>(IntPtr library, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlUtilization
    {
        public uint Gpu;
        public uint Memory;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvmlInit();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvmlShutdown();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temperature);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvmlDeviceGetUtilizationRates(IntPtr device, out NvmlUtilization utilization);
}
