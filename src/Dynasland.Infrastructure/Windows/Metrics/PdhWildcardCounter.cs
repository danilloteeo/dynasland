using System.Runtime.InteropServices;

namespace Dynasland.Infrastructure.Windows.Metrics;

/// <summary>
/// Contador de desempenho (PDH) com curinga de instâncias, ex.: <c>\GPU Engine(*)\Utilization Percentage</c>.
/// Contadores de taxa só têm valor a partir da segunda coleta.
/// </summary>
internal sealed class PdhWildcardCounter : IDisposable
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhFmtNoCap100 = 0x00008000;
    private const int PdhMoreData = unchecked((int)0x800007D2);
    private const uint PdhCstatusValidData = 0;
    private const uint PdhCstatusNewData = 1;

    private IntPtr _query;
    private readonly IntPtr _counter;

    private PdhWildcardCounter(IntPtr query, IntPtr counter)
    {
        _query = query;
        _counter = counter;
    }

    /// <returns><c>null</c> se o contador não existir neste computador.</returns>
    public static PdhWildcardCounter? TryCreate(string englishPath)
    {
        if (PdhOpenQuery(null, IntPtr.Zero, out var query) != 0)
        {
            return null;
        }

        if (PdhAddEnglishCounter(query, englishPath, IntPtr.Zero, out var counter) != 0 || PdhCollectQueryData(query) != 0)
        {
            PdhCloseQuery(query);
            return null;
        }

        return new PdhWildcardCounter(query, counter);
    }

    /// <summary>Coleta e devolve (instância, valor) das instâncias com dado válido.</summary>
    public IReadOnlyList<(string Instance, double Value)> Read()
    {
        if (_query == IntPtr.Zero || PdhCollectQueryData(_query) != 0)
        {
            return [];
        }

        uint bufferSize = 0;
        var status = PdhGetFormattedCounterArray(_counter, PdhFmtDouble | PdhFmtNoCap100, ref bufferSize, out _, IntPtr.Zero);
        if (status != PdhMoreData || bufferSize == 0)
        {
            return [];
        }

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            if (PdhGetFormattedCounterArray(_counter, PdhFmtDouble | PdhFmtNoCap100, ref bufferSize, out var count, buffer) != 0)
            {
                return [];
            }

            var itemSize = Marshal.SizeOf<PdhFmtCounterValueItemDouble>();
            var results = new List<(string, double)>((int)count);
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<PdhFmtCounterValueItemDouble>(buffer + i * itemSize);
                if (item.Value.CStatus is PdhCstatusValidData or PdhCstatusNewData)
                {
                    results.Add((Marshal.PtrToStringUni(item.Name) ?? string.Empty, item.Value.DoubleValue));
                }
            }

            return results;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValueDouble
    {
        public uint CStatus;
        public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValueItemDouble
    {
        public IntPtr Name;
        public PdhFmtCounterValueDouble Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhAddEnglishCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern int PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    private static extern int PdhCloseQuery(IntPtr query);
}
