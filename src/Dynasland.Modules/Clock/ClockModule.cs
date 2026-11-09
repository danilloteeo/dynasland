using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;

namespace Dynasland.Modules.Clock;

/// <summary>
/// Atividade contínua de baixa prioridade com hora e data: é o que a ilha mostra ao ser
/// aberta quando nenhum outro módulo tem algo a exibir. Atualiza uma vez por minuto.
/// </summary>
public sealed class ClockModule(IEventBus bus, IUiDispatcher ui, TimeProvider time, IShortcutRegistry shortcuts) : IIslandModule, IDisposable
{
    public const string ModuleId = "clock";
    public const string ActivityId = "clock.now";

    private readonly Lock _lock = new();
    private ClockContent? _content;
    private ITimer? _timer;

    public string Id => ModuleId;

    public string DisplayName => "Relógio";

    public string Description => "Tela inicial ao abrir a ilha: hora, data e atalhos dos módulos ativos.";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Ainda não está ligado à interface: pode ser preenchido fora da thread de UI.
        var content = new ClockContent();
        content.Update(time.GetLocalNow());
        content.SetShortcuts(shortcuts.Shortcuts);
        shortcuts.Changed += OnShortcutsChanged;

        lock (_lock)
        {
            _content = content;
            ScheduleNextTick();
        }

        bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Live, content)
        {
            Priority = ActivityPriority.Low,
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    private void OnShortcutsChanged(object? sender, EventArgs e)
    {
        ClockContent? content;
        lock (_lock)
        {
            content = _content;
        }

        if (content is not null)
        {
            var current = shortcuts.Shortcuts;
            ui.Post(() => content.SetShortcuts(current));
        }
    }

    public void Dispose()
    {
        shortcuts.Changed -= OnShortcutsChanged;
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _content = null;
        }
    }

    /// <summary>Agenda o próximo tick para a virada do minuto. Chamar com <see cref="_lock"/> adquirido.</summary>
    private void ScheduleNextTick()
    {
        var now = time.GetLocalNow();
        var intoMinute = TimeSpan.FromTicks(now.TimeOfDay.Ticks % TimeSpan.TicksPerMinute);
        var delay = TimeSpan.FromMinutes(1) - intoMinute + TimeSpan.FromMilliseconds(20);

        _timer?.Dispose();
        _timer = time.CreateTimer(_ => OnTick(), null, delay, Timeout.InfiniteTimeSpan);
    }

    private void OnTick()
    {
        ClockContent? content;
        lock (_lock)
        {
            content = _content;
            if (content is null)
            {
                return;
            }

            ScheduleNextTick();
        }

        var now = time.GetLocalNow();
        ui.Post(() => content.Update(now));
    }
}
