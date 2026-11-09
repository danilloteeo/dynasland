using System.Windows;
using System.Windows.Threading;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;

namespace Dynasland.App.Services;

/// <summary>
/// Hospeda o <see cref="IslandCoordinator"/> na thread de UI: recebe eventos dos módulos
/// (de qualquer thread), repassa as interações do usuário e agenda um único timer
/// para o próximo prazo do coordenador (sem polling).
/// </summary>
public sealed class IslandController : IDisposable
{
    private static readonly TimeSpan MinimumDelay = TimeSpan.FromMilliseconds(1);

    private readonly IslandCoordinator _coordinator;
    private readonly TimeProvider _time;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _deadlineTimer;
    private readonly IDisposable[] _subscriptions;

    public IslandController(IEventBus bus, IIslandSettingsService settings, TimeProvider time)
    {
        _time = time;
        _dispatcher = Application.Current.Dispatcher;
        _coordinator = new IslandCoordinator(() => settings.Current);
        _coordinator.Changed += (_, snapshot) => Changed?.Invoke(this, snapshot);

        _deadlineTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher);
        _deadlineTimer.Tick += (_, _) => Run(c => c.Advance(Now));

        _subscriptions =
        [
            bus.Subscribe<ActivityPostedEvent>(e => Run(c => c.Post(e.Activity, Now))),
            bus.Subscribe<ActivityRemovedEvent>(e => Run(c => c.Remove(e.ActivityId, Now))),
            bus.Subscribe<ModuleStoppedEvent>(e => Run(c => c.RemoveModule(e.ModuleId, Now))),
        ];
    }

    /// <summary>Sempre disparado na thread de UI.</summary>
    public event EventHandler<IslandSnapshot>? Changed;

    public IslandSnapshot Snapshot => _coordinator.Snapshot;

    private DateTimeOffset Now => _time.GetUtcNow();

    public void ToggleInteractive() => Run(c => c.ToggleInteractive(Now));

    public void Cycle(int direction) => Run(c => c.Cycle(direction, Now));

    public void SelectPage(int index) => Run(c => c.SelectPage(index, Now));

    public void PointerEntered() => Run(c => c.PointerEntered());

    public void PointerExited() => Run(c => c.PointerExited(Now));

    public void Reset() => Run(c => c.Reset());

    private void Run(Action<IslandCoordinator> action)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => Run(action));
            return;
        }

        action(_coordinator);
        ScheduleDeadline();
    }

    private void ScheduleDeadline()
    {
        _deadlineTimer.Stop();
        if (_coordinator.NextDeadline is not { } deadline)
        {
            return;
        }

        var delay = deadline - Now;
        _deadlineTimer.Interval = delay > MinimumDelay ? delay : MinimumDelay;
        _deadlineTimer.Start();
    }

    public void Dispose()
    {
        _deadlineTimer.Stop();
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
    }
}
