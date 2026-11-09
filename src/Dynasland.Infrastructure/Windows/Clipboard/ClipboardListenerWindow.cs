using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Dynasland.Infrastructure.Windows.Clipboard;

/// <summary>
/// Janela "somente mensagens" numa thread própria que recebe WM_CLIPBOARDUPDATE via
/// AddClipboardFormatListener: notificação do Windows a cada cópia, sem polling e sem depender da UI.
/// </summary>
internal sealed class ClipboardListenerWindow : IDisposable
{
    private const uint WmClipboardUpdate = 0x031D;
    private const uint WmQuit = 0x0012;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly Action<IntPtr> _onClipboardUpdate;
    private readonly WndProc _wndProc;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private readonly string _className = "DynaslandClipboardListener_" + Guid.NewGuid().ToString("N");
    private uint _threadId;
    private Exception? _startError;

    private ClipboardListenerWindow(Action<IntPtr> onClipboardUpdate)
    {
        _onClipboardUpdate = onClipboardUpdate;
        _wndProc = WindowProcedure; // mantido em campo para não ser coletado enquanto a janela existir
        _thread = new Thread(Run) { IsBackground = true, Name = "Dynasland.Clipboard" };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    /// <param name="onClipboardUpdate">Chamado na thread do listener com o HWND (útil para abrir a área de transferência).</param>
    public static ClipboardListenerWindow Start(Action<IntPtr> onClipboardUpdate)
    {
        var listener = new ClipboardListenerWindow(onClipboardUpdate);
        listener._thread.Start();
        listener._ready.Wait();
        if (listener._startError is not null)
        {
            throw listener._startError;
        }

        return listener;
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        var hInstance = GetModuleHandle(null);
        var windowClass = new WndClassEx
        {
            Size = (uint)Marshal.SizeOf<WndClassEx>(),
            WndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            Instance = hInstance,
            ClassName = _className,
        };

        IntPtr hwnd = IntPtr.Zero;
        try
        {
            if (RegisterClassEx(ref windowClass) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            hwnd = CreateWindowEx(0, _className, string.Empty, 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, hInstance, IntPtr.Zero);
            if (hwnd == IntPtr.Zero || !AddClipboardFormatListener(hwnd))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch (Exception ex)
        {
            _startError = ex;
            _ready.Set();
            return;
        }

        _ready.Set();

        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        RemoveClipboardFormatListener(hwnd);
        DestroyWindow(hwnd);
        UnregisterClass(_className, hInstance);
    }

    private IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmClipboardUpdate)
        {
            try
            {
                _onClipboardUpdate(hwnd);
            }
            catch
            {
                // Exceções não podem atravessar a fronteira nativa da window procedure.
            }

            return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_threadId != 0)
        {
            PostThreadMessage(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
            _thread.Join(TimeSpan.FromSeconds(2));
            _threadId = 0;
        }

        _ready.Dispose();
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public IntPtr WndProc;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WndClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClass(string className, IntPtr instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg message, IntPtr hwnd, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg message);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
