using Dynasland.Core.Activities;

namespace Dynasland.Tests.Core;

public class IslandAlertPulseTests
{
    private static IslandActivity Activity(ActivityKind kind, ActivityPriority priority) =>
        new("id", "test", kind, new StatusContent(StatusKind.Info, "Aviso")) { Priority = priority };

    [Theory]
    [InlineData(ActivityPriority.High)]
    [InlineData(ActivityPriority.Critical)]
    public void Transient_alert_pulses(ActivityPriority priority) =>
        Assert.True(IslandAlertPulse.ShouldPulse(Activity(ActivityKind.Transient, priority)));

    [Theory]
    [InlineData(ActivityPriority.Low)]
    [InlineData(ActivityPriority.Normal)]
    public void Ordinary_transient_does_not_pulse(ActivityPriority priority) =>
        Assert.False(IslandAlertPulse.ShouldPulse(Activity(ActivityKind.Transient, priority)));

    [Theory]
    [InlineData(ActivityPriority.Normal)]
    [InlineData(ActivityPriority.High)]
    [InlineData(ActivityPriority.Critical)]
    public void Live_activity_does_not_pulse(ActivityPriority priority) =>
        Assert.False(IslandAlertPulse.ShouldPulse(Activity(ActivityKind.Live, priority)));
}
