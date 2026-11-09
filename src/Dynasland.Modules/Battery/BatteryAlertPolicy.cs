using Dynasland.Core.Activities;
using Dynasland.Core.Models;

namespace Dynasland.Modules.Battery;

public enum BatteryAlert
{
    None,
    PluggedIn,
    Unplugged,
    Low,
    Critical,
}

/// <summary>Decide quando uma mudança de energia merece aparecer na ilha.</summary>
public static class BatteryAlertPolicy
{
    public const int LowThreshold = 20;
    public const int CriticalThreshold = 10;

    /// <summary>Limite de bateria muito fraca: 10% ou metade do limite de bateria fraca, o que for menor.</summary>
    public static int CriticalFor(int lowThreshold) => Math.Min(CriticalThreshold, lowThreshold / 2);

    public static BatteryAlert Evaluate(PowerState previous, PowerState current, int lowThreshold = LowThreshold)
    {
        if (!current.HasBattery)
        {
            return BatteryAlert.None;
        }

        if (!previous.IsPluggedIn && current.IsPluggedIn)
        {
            return BatteryAlert.PluggedIn;
        }

        if (previous.IsPluggedIn && !current.IsPluggedIn)
        {
            return BatteryAlert.Unplugged;
        }

        // Avisa uma vez ao cruzar cada limite, descarregando.
        if (!current.IsPluggedIn)
        {
            var critical = CriticalFor(lowThreshold);
            if (previous.Percent > critical && current.Percent <= critical)
            {
                return BatteryAlert.Critical;
            }

            if (previous.Percent > lowThreshold && current.Percent <= lowThreshold)
            {
                return BatteryAlert.Low;
            }
        }

        return BatteryAlert.None;
    }

    public static (ActivityPriority Priority, TimeSpan Duration) PresentationFor(BatteryAlert alert) => alert switch
    {
        BatteryAlert.Critical => (ActivityPriority.Critical, TimeSpan.FromSeconds(10)),
        BatteryAlert.Low => (ActivityPriority.High, TimeSpan.FromSeconds(6)),
        _ => (ActivityPriority.Normal, TimeSpan.FromSeconds(3)),
    };

    public static string HeadlineFor(BatteryAlert alert, PowerState state) => alert switch
    {
        BatteryAlert.Critical => "Bateria muito fraca",
        BatteryAlert.Low => "Bateria fraca",
        BatteryAlert.Unplugged => "Usando bateria",
        BatteryAlert.PluggedIn when state.BatteryState == BatteryChargeState.Charging => "Carregando",
        BatteryAlert.PluggedIn => "Conectado à energia",
        _ => StateText(state),
    };

    public static string StateText(PowerState state) => state.BatteryState switch
    {
        BatteryChargeState.Charging => "Carregando",
        BatteryChargeState.Idle => "Conectado, sem carregar",
        BatteryChargeState.Discharging => "Usando bateria",
        _ => "Sem bateria",
    };
}
