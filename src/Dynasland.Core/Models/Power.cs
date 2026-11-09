namespace Dynasland.Core.Models;

public enum BatteryChargeState
{
    NotPresent,
    Discharging,

    /// <summary>Conectado à energia, sem carregar (bateria cheia ou carga pausada).</summary>
    Idle,
    Charging,
}

/// <param name="RemainingTime">Autonomia estimada pelo Windows; <c>null</c> se indisponível ou carregando.</param>
public sealed record PowerState(BatteryChargeState BatteryState, int Percent, bool IsPluggedIn, TimeSpan? RemainingTime)
{
    public bool HasBattery => BatteryState != BatteryChargeState.NotPresent;
}
