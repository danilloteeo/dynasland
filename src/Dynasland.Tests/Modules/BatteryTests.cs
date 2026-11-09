using Dynasland.Core.Activities;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Battery;
using Dynasland.Modules.Common;

namespace Dynasland.Tests.Modules;

public class BatteryTests : IDisposable
{
    private readonly ActivityRecorder _recorder = new();

    private static PowerState OnBattery(int percent) => new(BatteryChargeState.Discharging, percent, false, TimeSpan.FromHours(2));

    private static PowerState Charging(int percent) => new(BatteryChargeState.Charging, percent, true, null);

    public void Dispose() => _recorder.Dispose();

    [Theory]
    [InlineData(21, 20, BatteryAlert.Low)]
    [InlineData(20, 19, BatteryAlert.None)]
    [InlineData(11, 10, BatteryAlert.Critical)]
    [InlineData(25, 9, BatteryAlert.Critical)]
    [InlineData(50, 49, BatteryAlert.None)]
    public void Low_battery_alerts_fire_once_when_crossing_thresholds(int before, int after, BatteryAlert expected)
    {
        Assert.Equal(expected, BatteryAlertPolicy.Evaluate(OnBattery(before), OnBattery(after)));
    }

    [Fact]
    public void Plugging_and_unplugging_are_reported()
    {
        Assert.Equal(BatteryAlert.PluggedIn, BatteryAlertPolicy.Evaluate(OnBattery(50), Charging(50)));
        Assert.Equal(BatteryAlert.Unplugged, BatteryAlertPolicy.Evaluate(Charging(50), OnBattery(50)));
        Assert.Equal(BatteryAlert.None, BatteryAlertPolicy.Evaluate(Charging(15), Charging(9)));
    }

    [Fact]
    public void No_alerts_without_battery()
    {
        var none = new PowerState(BatteryChargeState.NotPresent, 100, true, null);

        Assert.Equal(BatteryAlert.None, BatteryAlertPolicy.Evaluate(none, none with { IsPluggedIn = false }));
    }

    [Fact]
    public async Task Module_is_unavailable_on_desktops()
    {
        var module = new BatteryModule(new FakePowerService(new PowerState(BatteryChargeState.NotPresent, 100, true, null)), _recorder.Bus, new InlineDispatcher());

        await Assert.ThrowsAsync<ModuleUnavailableException>(() => module.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Module_announces_alerts_with_priority_and_details()
    {
        var power = new FakePowerService(OnBattery(22));
        var module = new BatteryModule(power, _recorder.Bus, new InlineDispatcher());
        await module.StartAsync(CancellationToken.None);

        power.Raise(OnBattery(21));
        Assert.Empty(_recorder.Posted);

        power.Raise(OnBattery(20));
        var low = Assert.Single(_recorder.Posted);
        Assert.Equal(ActivityPriority.High, low.Priority);
        var content = Assert.IsType<BatteryContent>(low.Content);
        Assert.Equal("Bateria fraca", content.Headline);
        Assert.True(content.IsLow);
        Assert.Equal("2 h restantes", content.RemainingText);

        power.Raise(Charging(20));
        Assert.Equal(2, _recorder.Posted.Count);
        Assert.Equal("Carregando", content.Headline);
        Assert.True(content.IsCharging);
        Assert.False(content.IsLow);
    }

    [Theory]
    [InlineData(0.5, "menos de 1 min")]
    [InlineData(40, "40 min")]
    [InlineData(120, "2 h")]
    [InlineData(135, "2 h 15 min")]
    public void Durations_are_formatted_in_portuguese(double minutes, string expected)
    {
        Assert.Equal(expected, TextFormat.Duration(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Media_times_are_formatted()
    {
        Assert.Equal("3:07", TextFormat.MediaTime(TimeSpan.FromSeconds(187)));
        Assert.Equal("1:02:45", TextFormat.MediaTime(new TimeSpan(1, 2, 45)));
    }
}
