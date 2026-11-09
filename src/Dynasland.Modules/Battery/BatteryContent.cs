using CommunityToolkit.Mvvm.ComponentModel;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Battery;

public sealed partial class BatteryContent : ObservableObject, IActivityContent
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fraction))]
    private int _percent;

    [ObservableProperty]
    private string _headline = string.Empty;

    [ObservableProperty]
    private string _stateText = string.Empty;

    [ObservableProperty]
    private string _remainingText = string.Empty;

    [ObservableProperty]
    private bool _isCharging;

    [ObservableProperty]
    private bool _isLow;

    public double Fraction => Percent / 100.0;

    public string ChargingGlyph => "";

    public void Apply(PowerState state, string headline, int lowThreshold = BatteryAlertPolicy.LowThreshold)
    {
        Percent = state.Percent;
        Headline = headline;
        StateText = BatteryAlertPolicy.StateText(state);
        IsCharging = state.BatteryState == BatteryChargeState.Charging;
        IsLow = !state.IsPluggedIn && state.Percent <= lowThreshold;
        RemainingText = state.RemainingTime is { } remaining ? $"{TextFormat.Duration(remaining)} restantes" : string.Empty;
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Expanded => new IslandDimensions(320, 54, 27),
        IslandState.Interactive => new IslandDimensions(360, 116, 32),
        _ => null,
    };
}
