using System.Runtime.InteropServices;
using System.Text;

namespace Dynasland.Infrastructure.Windows;

/// <summary>
/// Detecta quando a janela em primeiro plano ocupa um monitor inteiro (jogos, vídeos, apresentações),
/// usando ganchos de eventos do Windows (troca de janela ativa e mudança de posição/tamanho dela),
/// sem polling.
/// </summary>
/// <remarks>Deve ser iniciado numa thread com loop de mensagens (a thread de UI).</remarks>
public sealed class FullscreenDetector : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectLocationChange = 0x800B;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;
    private const int ObjIdWindow = 0;
    private const uint MonitorDefaultToNearest = 2;

    private static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

    private readonly WinEventProc _callback;
    private IntPtr _foregroundHook;
    private IntPtr _locationHook;

    public FullscreenDetector() => _callback = OnWinEvent;

    /// <summary>Monitor (nome do dispositivo) ocupado por um aplicativo em tela cheia; <c>null</c> se nenhum.</summary>
    public string? FullscreenMonitor { get; private set; }

    public event EventHandler<string?>? Changed;

    public void Start()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            return;
        }

        const uint flags = WinEventOutOfContext | WinEventSkipOwnProcess;
        _foregroundHook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, _callback, 0, 0, flags);
        _locationHook = SetWinEventHook(EventObjectLocationChange, EventObjectLocationChange, IntPtr.Zero, _callback, 0, 0, flags);
        Evaluate();
    }

    public void Dispose()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }

        if (_locationHook != IntPtr.Zero)
        {
            UnhookWinEvent(_locationHook);
            _locationHook = IntPtr.Zero;
        }
    }

    /// <summary>Uma janela cobre o monitor inteiro e não é a área de trabalho nem a barra de tarefas.</summary>
    internal static string? FullscreenMonitorOf(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd))
        {
            return null;
        }

        var className = new StringBuilder(64);
        GetClassName(hwnd, className, className.Capacity);
        if (ShellClasses.Contains(className.ToString()) || !GetWindowRect(hwnd, out var window))
        {
            return null;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return null;
        }

        var covers = window.Left <= info.Monitor.Left && window.Top <= info.Monitor.Top
            && window.Right >= info.Monitor.Right && window.Bottom >= info.Monitor.Bottom;
        return covers ? info.DeviceName : null;
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Mudanças de posição são muito frequentes (cursor, janelas filhas); só interessa a janela ativa.
        if (eventType == EventObjectLocationChange && (idObject != ObjIdWindow || hwnd != GetForegroundWindow()))
        {
            return;
        }

        Evaluate();
    }

    private void Evaluate()
    {
        var monitor = FullscreenMonitorOf(GetForegroundWindow());
        if (monitor == FullscreenMonitor)
        {
            return;
        }

        FullscreenMonitor = monitor;
        Changed?.Invoke(this, monitor);
    }

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);
}
