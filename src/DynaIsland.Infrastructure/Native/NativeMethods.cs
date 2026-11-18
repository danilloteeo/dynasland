using System.Runtime.InteropServices;

namespace DynaIsland.Infrastructure.Native;

/// <summary>Espelho de dynaisland_engine.h (API 1). Qualquer mudança lá exige mudar aqui e subir a versão.</summary>
internal enum NativeResult
{
    Ok = 0,
    InvalidArgument = 1,
    VersionMismatch = 2,
    AlreadyStarted = 3,
    NotStarted = 4,
    HookFailed = 5,
    Internal = 6,
}

internal static class NativeMethods
{
    private const string Library = "DynaIslandEngine.dll";

    /// <summary>struct di_event: 32 bytes, campos na ordem do cabeçalho.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Event
    {
        public uint Size;
        public uint Type;
        public int X;
        public int Y;
        public ulong Window;
        public uint Button;
        public uint Reserved;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void EventCallback(IntPtr nativeEvent, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void LogCallback(int level, IntPtr utf8Message, IntPtr userData);

    [DllImport(Library, EntryPoint = "di_api_version", CallingConvention = CallingConvention.StdCall)]
    internal static extern uint ApiVersion();

    [DllImport(Library, EntryPoint = "di_start", CallingConvention = CallingConvention.StdCall)]
    internal static extern NativeResult Start(uint expectedApiVersion, EventCallback onEvent, IntPtr eventUserData, LogCallback? onLog, IntPtr logUserData);

    [DllImport(Library, EntryPoint = "di_stop", CallingConvention = CallingConvention.StdCall)]
    internal static extern NativeResult Stop();

    [DllImport(Library, EntryPoint = "di_set_input_hooks", CallingConvention = CallingConvention.StdCall)]
    internal static extern NativeResult SetInputHooks(int enabled);

    [DllImport(Library, EntryPoint = "di_result_message", CallingConvention = CallingConvention.StdCall)]
    internal static extern IntPtr ResultMessage(NativeResult result);
}
