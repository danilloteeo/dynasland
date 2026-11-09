using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Windows.System.Power;

namespace Dynasland.Infrastructure.Windows.Power;

/// <summary>Bateria e fonte de energia via Windows.System.Power.PowerManager (orientado a eventos).</summary>
public sealed class WindowsPowerService : IPowerService
{
    private static readonly TimeSpan MaxPlausibleRemaining = TimeSpan.FromDays(2);

    private readonly Lock _lock = new();

    public PowerState Current { get; private set; } = Read();

    public event EventHandler<PowerState>? Changed;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Current = Read();
        PowerManager.BatteryStatusChanged += OnPowerChanged;
        PowerManager.RemainingChargePercentChanged += OnPowerChanged;
        PowerManager.PowerSupplyStatusChanged += OnPowerChanged;
        PowerManager.RemainingDischargeTimeChanged += OnPowerChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        PowerManager.BatteryStatusChanged -= OnPowerChanged;
        PowerManager.RemainingChargePercentChanged -= OnPowerChanged;
        PowerManager.PowerSupplyStatusChanged -= OnPowerChanged;
        PowerManager.RemainingDischargeTimeChanged -= OnPowerChanged;
        return Task.CompletedTask;
    }

    private void OnPowerChanged(object? sender, object e)
    {
        PowerState state;
        lock (_lock)
        {
            state = Read();
            if (state == Current)
            {
                return;
            }

            Current = state;
        }

        Changed?.Invoke(this, state);
    }

    private static PowerState Read()
    {
        var batteryState = PowerManager.BatteryStatus switch
        {
            BatteryStatus.Discharging => BatteryChargeState.Discharging,
            BatteryStatus.Idle => BatteryChargeState.Idle,
            BatteryStatus.Charging => BatteryChargeState.Charging,
            _ => BatteryChargeState.NotPresent,
        };

        var remaining = PowerManager.RemainingDischargeTime;
        TimeSpan? estimate = batteryState == BatteryChargeState.Discharging && remaining > TimeSpan.Zero && remaining < MaxPlausibleRemaining
            ? remaining
            : null;

        return new PowerState(
            batteryState,
            Math.Clamp(PowerManager.RemainingChargePercent, 0, 100),
            PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent,
            estimate);
    }
}
