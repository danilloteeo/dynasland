using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;

namespace Dynasland.Modules.Battery;

/// <summary>Avisos de carregamento, desconexão da energia e bateria fraca (somente com bateria).</summary>
public sealed class BatteryModule(IPowerService power, IEventBus bus, IUiDispatcher ui, IModuleOptions options) : IIslandModule
{
    public const string ModuleId = "battery";
    public const string ActivityId = "battery.status";

    private readonly Lock _lock = new();
    private BatteryContent? _content;
    private PowerState? _last;

    public string Id => ModuleId;

    public string DisplayName => "Bateria";

    public string Description => "Carregamento e avisos de bateria fraca (notebooks).";

    public static readonly ModuleOption LowThresholdOption = ModuleOption.Slider(
        "lowThreshold", "Aviso de bateria fraca", "Carga em que a ilha avisa que a bateria está acabando.", BatteryAlertPolicy.LowThreshold, 10, 40, 5, "%");

    public IReadOnlyList<ModuleOption> Options => [LowThresholdOption];

    private int LowThreshold => (int)options.Get(ModuleId, LowThresholdOption);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await power.StartAsync(cancellationToken);
        var initial = power.Current;
        if (!initial.HasBattery)
        {
            await power.StopAsync();
            throw new ModuleUnavailableException("Este computador não possui bateria.");
        }

        var content = new BatteryContent();
        lock (_lock)
        {
            _content = content;
            _last = initial;
        }

        ui.Post(() => content.Apply(initial, BatteryAlertPolicy.StateText(initial)));
        power.Changed += OnPowerChanged;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        power.Changed -= OnPowerChanged;
        await power.StopAsync();
        lock (_lock)
        {
            _content = null;
            _last = null;
        }
    }

    private void OnPowerChanged(object? sender, PowerState state)
    {
        BatteryContent content;
        BatteryAlert alert;
        lock (_lock)
        {
            if (_content is null || _last is null)
            {
                return;
            }

            content = _content;
            alert = BatteryAlertPolicy.Evaluate(_last, state, LowThreshold);
            _last = state;
        }

        var headline = BatteryAlertPolicy.HeadlineFor(alert, state);
        var threshold = LowThreshold;
        ui.Post(() => content.Apply(state, headline, threshold));

        if (alert == BatteryAlert.None)
        {
            return;
        }

        var (priority, duration) = BatteryAlertPolicy.PresentationFor(alert);
        bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Transient, content)
        {
            Priority = priority,
            Duration = duration,
        });
    }
}
