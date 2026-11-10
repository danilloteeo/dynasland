using System.Windows.Threading;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Dynasland.App.Services;

/// <summary>
/// Observa bloqueio de sessão (<see cref="SystemEvents.SessionSwitch"/>) e suspensão/retomada
/// (<see cref="SystemEvents.PowerModeChanged"/>), mantém o <see cref="SessionState"/> e publica
/// <see cref="SessionChangedEvent"/> no barramento para os módulos.
/// </summary>
/// <remarks><see cref="Start"/> deve ser chamado na thread de UI (o SystemEvents usa o loop de mensagens dela).</remarks>
public sealed class SessionStateService(IEventBus bus, ILogger<SessionStateService> logger) : ISessionStateService, IDisposable
{
    private readonly SessionState _state = new();
    private Dispatcher? _dispatcher;

    public bool IsInterrupted => _state.IsInterrupted;

    public event EventHandler<SessionChangedEvent>? Changed;

    public void Start()
    {
        if (_dispatcher is not null)
        {
            return;
        }

        _dispatcher = Dispatcher.CurrentDispatcher;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Dispose()
    {
        if (_dispatcher is null)
        {
            return;
        }

        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _dispatcher = null;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                Apply(SessionChange.Locked);
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
                Apply(SessionChange.Unlocked);
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                Apply(SessionChange.Suspending);
                break;
            case PowerModes.Resume:
                Apply(SessionChange.Resumed);
                break;
        }
    }

    private void Apply(SessionChange change)
    {
        if (_dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Apply(change));
            return;
        }

        _state.Apply(change);
        logger.LogInformation("Sessão: {Change} (ilha {State}).", change, _state.IsInterrupted ? "oculta" : "liberada");

        var evt = new SessionChangedEvent(change, _state.IsInterrupted);
        Changed?.Invoke(this, evt);
        bus.Publish(evt);
    }
}
