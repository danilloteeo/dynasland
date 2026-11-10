using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using Dynasland.Core.Shortcuts;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.Services;

/// <summary>
/// Registra no Windows (<c>RegisterHotKey</c>) as combinações do <see cref="IGlobalShortcutRegistry"/>,
/// numa janela só de mensagens, e executa a ação quando a combinação é pressionada em qualquer programa.
/// Combinações já usadas por outro programa são informadas de volta ao catálogo (aparecem nas configurações).
/// </summary>
public sealed class HotkeyService(IGlobalShortcutRegistry registry, ILogger<HotkeyService> logger) : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private readonly Dictionary<int, GlobalShortcut> _registered = [];
    private HwndSource? _window;
    private Dispatcher? _dispatcher;
    private bool _refreshPending;

    /// <summary>Enquanto o usuário grava uma nova combinação nas configurações, os atalhos não disparam.</summary>
    public bool IsPaused { get; set; }

    /// <summary>Chamar na thread de UI.</summary>
    public void Start()
    {
        if (_window is not null)
        {
            return;
        }

        _dispatcher = Dispatcher.CurrentDispatcher;
        _window = new HwndSource(new HwndSourceParameters("DynaslandHotkeys")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE: janela invisível, só recebe mensagens.
            WindowStyle = 0,
        });
        _window.AddHook(WndProc);
        registry.Changed += OnRegistryChanged;
        Refresh();
    }

    public void Dispose()
    {
        registry.Changed -= OnRegistryChanged;
        UnregisterAll();
        _window?.Dispose();
        _window = null;
    }

    private void OnRegistryChanged(object? sender, EventArgs e)
    {
        // O catálogo muda em rajadas (módulos iniciando); agrupa num só registro.
        if (_dispatcher is null || _refreshPending)
        {
            return;
        }

        _refreshPending = true;
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _refreshPending = false;
            Refresh();
        });
    }

    private void Refresh()
    {
        if (_window is null)
        {
            return;
        }

        UnregisterAll();
        var id = 1;
        foreach (var binding in registry.Bindings)
        {
            if (binding.Gesture is not { } gesture)
            {
                registry.ReportRegistration(binding.Shortcut.Id, null);
                continue;
            }

            if (RegisterHotKey(_window.Handle, id, (uint)gesture.Modifiers | ModNoRepeat, (uint)gesture.Key))
            {
                _registered[id++] = binding.Shortcut;
                registry.ReportRegistration(binding.Shortcut.Id, null);
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                var message = error == ErrorHotkeyAlreadyRegistered ? "Em uso por outro programa" : $"Não foi possível registrar (erro {error})";
                logger.LogWarning("Atalho {Gesture} de {Shortcut}: {Message}.", gesture, binding.Shortcut.Id, message);
                registry.ReportRegistration(binding.Shortcut.Id, message);
            }
        }
    }

    private void UnregisterAll()
    {
        if (_window is null)
        {
            return;
        }

        foreach (var id in _registered.Keys)
        {
            UnregisterHotKey(_window.Handle, id);
        }

        _registered.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _registered.TryGetValue(wParam.ToInt32(), out var shortcut))
        {
            handled = true;
            if (!IsPaused)
            {
                try
                {
                    shortcut.Invoke();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Falha no atalho {Shortcut}.", shortcut.Id);
                }
            }
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
