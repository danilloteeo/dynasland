using System.Runtime.InteropServices;
using Dynasland.Core.Interfaces;

namespace Dynasland.Infrastructure.Windows;

/// <summary>
/// Estado de Caps Lock e Num Lock lido com GetKeyState (bit de alternância), a cada 250 ms enquanto ligado.
/// Não instala hook de teclado e não lê o que é digitado.
/// </summary>
public sealed class KeyLockService : IKeyLockService, IDisposable
{
    private const int VkCapital = 0x14;
    private const int VkNumLock = 0x90;
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    private readonly Lock _lock = new();
    private Timer? _timer;
    private KeyLockState _current = Read();

    public KeyLockState Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public event EventHandler<KeyLockChangedEventArgs>? Changed;

    public void Start()
    {
        lock (_lock)
        {
            _current = Read();
            _timer ??= new Timer(_ => Poll(), null, Interval, Interval);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    public void Dispose() => Stop();

    private void Poll()
    {
        var next = Read();
        KeyLockChangedEventArgs? caps = null;
        KeyLockChangedEventArgs? num = null;
        lock (_lock)
        {
            if (_timer is null)
            {
                return;
            }

            if (next.CapsLock != _current.CapsLock)
            {
                caps = new KeyLockChangedEventArgs("Caps Lock", next.CapsLock, next);
            }

            if (next.NumLock != _current.NumLock)
            {
                num = new KeyLockChangedEventArgs("Num Lock", next.NumLock, next);
            }

            _current = next;
        }

        if (caps is not null)
        {
            Changed?.Invoke(this, caps);
        }

        if (num is not null)
        {
            Changed?.Invoke(this, num);
        }
    }

    private static KeyLockState Read() => new((GetKeyState(VkCapital) & 1) != 0, (GetKeyState(VkNumLock) & 1) != 0);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}
