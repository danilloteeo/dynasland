using Dynasland.Core.Activities;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Battery;
using Dynasland.Modules.Media;
using Dynasland.Modules.Notifications;
using Dynasland.Modules.SystemMonitor;
using Dynasland.Modules.Volume;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

/// <summary>As opções exibidas nas configurações realmente mudam o comportamento dos módulos.</summary>
public class ModuleOptionEffectsTests : IDisposable
{
    private readonly ActivityRecorder _recorder = new();
    private readonly ModuleOptionsService _options = TestOptions.Create();

    public void Dispose() => _recorder.Dispose();

    [Fact]
    public async Task Media_does_not_announce_tracks_when_disabled()
    {
        var media = new FakeMediaService();
        var module = new MediaModule(media, _recorder.Bus, new InlineDispatcher(), new FakeTimeProvider(), _options, new FakeIslandSettings());
        await _options.SetAsync(MediaModule.ModuleId, MediaModule.AnnounceTracks, 0);
        await module.StartAsync(CancellationToken.None);

        media.Raise(FakeMediaService.Track("Nova música"));

        Assert.Empty(_recorder.PostedWithId(MediaModule.TrackActivityId));
        Assert.Single(_recorder.PostedWithId(MediaModule.SessionActivityId("Spotify.exe")));
    }

    [Fact]
    public async Task Equalizer_stops_when_option_or_animations_are_disabled()
    {
        var media = new FakeMediaService { Current = FakeMediaService.Track("A") };
        var settings = new FakeIslandSettings();
        var module = new MediaModule(media, _recorder.Bus, new InlineDispatcher(), new FakeTimeProvider(), _options, settings);
        await module.StartAsync(CancellationToken.None);
        var content = Assert.IsType<MediaContent>(_recorder.Posted.Single().Content);
        content.OnPresentationChanged(IslandState.Compact);
        Assert.True(content.IsIndicatorAnimating);

        await settings.UpdateAsync(s => s with { AnimationsEnabled = false });
        Assert.False(content.IsIndicatorAnimating);

        await settings.UpdateAsync(s => s with { AnimationsEnabled = true });
        Assert.True(content.IsIndicatorAnimating);

        await _options.SetAsync(MediaModule.ModuleId, MediaModule.AnimatedEqualizer, 0);
        Assert.False(content.IsIndicatorAnimating);
    }

    [Fact]
    public async Task Volume_change_is_not_shown_when_disabled()
    {
        var audio = new FakeAudioService();
        var module = new VolumeModule(audio, _recorder.Bus, new InlineDispatcher(), _options);
        await _options.SetAsync(VolumeModule.ModuleId, VolumeModule.ShowOnChange, 0);
        await module.StartAsync(CancellationToken.None);

        audio.Raise(new VolumeState(0.7f, false, null), VolumeChangeReason.External);

        Assert.Empty(_recorder.Posted);
    }

    [Fact]
    public async Task Battery_threshold_follows_the_slider()
    {
        var power = new FakePowerService(new PowerState(BatteryChargeState.Discharging, 32, false, null));
        var module = new BatteryModule(power, _recorder.Bus, new InlineDispatcher(), _options);
        await _options.SetAsync(BatteryModule.ModuleId, BatteryModule.LowThresholdOption, 30);
        await module.StartAsync(CancellationToken.None);

        power.Raise(new PowerState(BatteryChargeState.Discharging, 30, false, null));

        Assert.Equal("Bateria fraca", Assert.IsType<BatteryContent>(Assert.Single(_recorder.Posted).Content).Headline);
    }

    [Fact]
    public async Task Notifications_respect_privacy_duration_and_priority()
    {
        var store = new InMemorySettingsStore();
        var preferences = new NotificationPreferences(store);
        var service = new FakeNotificationService();
        var module = new NotificationsModule(service, preferences, _options, _recorder.Bus, NullLogger<NotificationsModule>.Instance);
        await module.StartAsync(CancellationToken.None);
        await preferences.SetDurationAsync(12);
        await preferences.SetCanInterruptAsync(true);
        await _options.SetAsync(NotificationsModule.ModuleId, NotificationsModule.ShowMessageText, 0);

        service.Receive(FakeNotificationService.Message(5, body: "senha do banco"));

        var activity = Assert.Single(_recorder.Posted);
        Assert.Equal(TimeSpan.FromSeconds(12), activity.Duration);
        Assert.Equal(ActivityPriority.High, activity.Priority);
        Assert.Equal("Conteúdo oculto", Assert.IsType<NotificationContent>(activity.Content).Body);
    }

    [Fact]
    public async Task Only_allowed_mode_shows_only_explicitly_allowed_apps()
    {
        var preferences = new NotificationPreferences(new InMemorySettingsStore());
        var service = new FakeNotificationService();
        var module = new NotificationsModule(service, preferences, _options, _recorder.Bus, NullLogger<NotificationsModule>.Instance);
        await module.StartAsync(CancellationToken.None);
        await preferences.SetModeAsync(NotificationFilterMode.OnlyAllowed);
        await preferences.SetRuleAsync("Teams", show: true);

        service.Receive(FakeNotificationService.Message(1, appId: "WhatsApp"));
        service.Receive(FakeNotificationService.Message(2, appId: "Teams"));

        Assert.Equal(["notification.2"], _recorder.Posted.Select(a => a.Id));
        await WaitUntilAsync(() => preferences.KnownApps.Count == 2);
        Assert.Contains(preferences.KnownApps, a => a.AppId == "WhatsApp" && a.Rule is null);
    }

    [Fact]
    public async Task Legacy_blocked_list_is_migrated_to_rules()
    {
        var store = new InMemorySettingsStore();
        store.Values[NotificationPreferences.LegacyBlockedAppsKey] = "Discord\nSpotify";
        var preferences = new NotificationPreferences(store);

        await preferences.LoadAsync();

        Assert.True(preferences.IsBlocked("Discord"));
        Assert.True(preferences.IsBlocked("spotify"));
        Assert.False(preferences.IsBlocked("Teams"));
    }

    [Fact]
    public async Task System_alerts_can_be_disabled_and_thresholds_adjusted()
    {
        var time = new FakeTimeProvider();
        var metrics = new ThresholdMetrics(freePercent: 12);
        var module = new SystemMonitorModule(metrics, _recorder.Bus, new InlineDispatcher(), time, new ShortcutRegistry(), _options, NullLogger<SystemMonitorModule>.Instance);
        await module.StartAsync(CancellationToken.None);

        time.Advance(TimeSpan.FromSeconds(10));
        Assert.Empty(_recorder.Posted); // 12% livres não passa do padrão (10%)

        await _options.SetAsync(SystemMonitorModule.ModuleId, SystemMonitorModule.DiskThreshold, 15);
        time.Advance(TimeSpan.FromSeconds(30));
        Assert.Single(_recorder.Posted);

        await _options.SetAsync(SystemMonitorModule.ModuleId, SystemMonitorModule.Alerts, 0);
        metrics.FreePercent = 2;
        time.Advance(TimeSpan.FromMinutes(5));
        Assert.Single(_recorder.Posted);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class ThresholdMetrics(double freePercent) : Dynasland.Core.Interfaces.ISystemMetricsService
    {
        private const ulong GB = 1024UL * 1024 * 1024;

        public double FreePercent { get; set; } = freePercent;

        public SystemMetrics Sample() => new(null, GetMemory(), null, null, null, null, GetDisks());

        public MemoryUsage GetMemory() => new(GB, 16 * GB);

        public IReadOnlyList<DiskUsage> GetDisks() =>
            [new DiskUsage(@"C:\", "", 100 * GB, (ulong)(FreePercent * GB), true)];
    }
}
