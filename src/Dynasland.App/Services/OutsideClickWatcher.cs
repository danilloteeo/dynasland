using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Dynasland.App.Services;

/// <summary>
/// Detecta cliques fora da ilha (e a tecla Esc) enquanto ela está aberta, com ganchos de mouse e de teclado
/// de baixo nível (<c>WH_MOUSE_LL</c>, <c>WH_KEYBOARD_LL</c>) instalados só durante o estado interativo. A janela da ilha não recebe foco,
/// então "perder o foco" não serve para isso.
/// </summary>
public sealed class OutsideClickWatcher : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const uint VkEscape = 0x1B;

    private readonly LowLevelMouseProc _callback;
    private readonly LowLevelMouseProc _keyCallback;
    private readonly Dispatcher _dispatcher;
    private IntPtr _hook;
    private IntPtr _keyHook;
    private Action? _onEscape;
    private Func<int, int, bool>? _isInside;
    private Action? _onOutsideClick;

    public OutsideClickWatcher()
    {
        _callback = HookCallback;
        _keyCallback = KeyCallback;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    /// <param name="isInside">Recebe o ponto em pixels físicos da tela; <c>true</c> se estiver sobre a ilha.</param>
    /// <param name="onEscape">Chamado ao apertar Esc (a tecla continua valendo para o aplicativo em foco).</param>
    public void Start(Func<int, int, bool> isInside, Action onOutsideClick, Action? onEscape = null)
    {
        _isInside = isInside;
        _onOutsideClick = onOutsideClick;
        _onEscape = onEscape;
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        using var module = Process.GetCurrentProcess().MainModule;
        var handle = GetModuleHandle(module?.ModuleName);
        _hook = SetWindowsHookEx(WhMouseLl, _callback, handle, 0);
        _keyHook = SetWindowsHookEx(WhKeyboardLl, _keyCallback, handle, 0);
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        if (_keyHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyHook);
            _keyHook = IntPtr.Zero;
        }
    }

    public void Dispose() => Stop();

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() is WmLButtonDown or WmRButtonDown or WmMButtonDown)
        {
            var info = Marshal.PtrToStructure<MouseHookInfo>(lParam);
            var x = info.X;
            var y = info.Y;

            // O gancho precisa responder rápido; a verificação roda logo depois, na fila da interface.
            _dispatcher.BeginInvoke(() =>
            {
                if (_hook != IntPtr.Zero && _isInside is { } isInside && !isInside(x, y))
                {
                    _onOutsideClick?.Invoke();
                }
            });
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private IntPtr KeyCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() == WmKeyDown && (uint)Marshal.ReadInt32(lParam) == VkEscape)
        {
            _dispatcher.BeginInvoke(() =>
            {
                if (_keyHook != IntPtr.Zero)
                {
                    _onEscape?.Invoke();
                }
            });
        }

        return CallNextHookEx(_keyHook, code, wParam, lParam);
    }

    private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookInfo
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelMouseProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
