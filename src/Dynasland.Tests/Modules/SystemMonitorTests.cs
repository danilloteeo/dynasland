using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Infrastructure.Windows.Metrics;
using Dynasland.Modules.SystemMonitor;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class SystemMonitorTests : IDisposable
{
    private const ulong GB = 1024UL * 1024 * 1024;

    private readonly FakeTimeProvider _time = new();
    private readonly ActivityRecorder _recorder = new();
    private readonly ShortcutRegistry _shortcuts = new();
    private readonly FakeMetrics _metrics = new();

    public void Dispose() => _recorder.Dispose();

    private SystemMonitorModule CreateModule() =>
        new(_metrics, _recorder.Bus, new InlineDispatcher(), _time, _shortcuts, NullLogger<SystemMonitorModule>.Instance);

    [Fact]
    public void Memory_alert_requires_two_high_readings_and_respects_cooldown()
    {
        var policy = new SystemAlertPolicy();
        var high = new MemoryUsage(15 * GB, 16 * GB);
        var now = DateTimeOffset.UnixEpoch;

        Assert.Empty(policy.Evaluate(high, [], now));
        Assert.Single(policy.Evaluate(high, [], now.AddSeconds(30)));
        Assert.Empty(policy.Evaluate(high, [], now.AddMinutes(5)));
        Assert.Single(policy.Evaluate(high, [], now.AddMinutes(20)));
    }

    [Fact]
    public void Disk_alert_fires_once_and_rearms_after_space_is_freed()
    {
        var policy = new SystemAlertPolicy();
        var memory = new MemoryUsage(GB, 16 * GB);
        var full = new DiskUsage(@"C:\", "", 500 * GB, 20 * GB, true);
        var freed = full with { FreeBytes = 100 * GB };

        var alert = Assert.Single(policy.Evaluate(memory, [full], DateTimeOffset.UnixEpoch));
        Assert.Equal("Pouco espaço em C:", alert.Title);
        Assert.Empty(policy.Evaluate(memory, [full], DateTimeOffset.UnixEpoch));
        Assert.Empty(policy.Evaluate(memory, [freed], DateTimeOffset.UnixEpoch));
        Assert.Single(policy.Evaluate(memory, [full], DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task Panel_samples_only_while_visible_and_shows_dash_for_missing_sensors()
    {
        var module = CreateModule();
        await module.StartAsync(CancellationToken.None);

        _shortcuts.Shortcuts.Single(s => s.Label == "Sistema").Invoke();
        var panel = Assert.Single(_recorder.Posted);
        Assert.True(panel.OpenInteractive);
        var content = Assert.IsType<SystemMonitorContent>(panel.Content);
        Assert.Equal(0, _metrics.FullSamples);

        content.OnPresentationChanged(IslandState.Interactive);
        _time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(4, _metrics.FullSamples);
        Assert.Equal("37%", content.CpuText);
        Assert.Equal(SystemMonitorContent.Unavailable, content.GpuText);
        Assert.Equal("Temperatura indisponível", content.CpuDetail);
        Assert.Equal("8 GB de 16 GB", content.MemoryDetail);

        content.OnPresentationChanged(null);
        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(4, _metrics.FullSamples);
    }

    [Fact]
    public async Task Background_check_posts_disk_alert()
    {
        _metrics.Disks = [new DiskUsage(@"D:\", "Dados", 1000 * GB, 30 * GB, false)];
        var module = CreateModule();
        await module.StartAsync(CancellationToken.None);

        _time.Advance(TimeSpan.FromSeconds(10));

        var alert = Assert.Single(_recorder.Posted);
        Assert.StartsWith("system.alert.disk.", alert.Id);
        Assert.Equal("Pouco espaço em D:", Assert.IsType<SystemAlertContent>(alert.Content).Title);
        Assert.Equal(0, _metrics.FullSamples);
    }

    [Theory]
    [InlineData("pid_1234_luid_0x00000000_0x0000C2E4_phys_0_eng_3_engtype_3D", "0x00000000_0x0000C2E4_phys_0_eng_3")]
    [InlineData("estranho", "estranho")]
    public void Gpu_engine_key_ignores_process(string instance, string expected)
    {
        Assert.Equal(expected, WindowsSystemMetricsService.EngineKey(instance));
    }

    private sealed class FakeMetrics : ISystemMetricsService
    {
        public int FullSamples { get; private set; }

        public IReadOnlyList<DiskUsage> Disks { get; set; } = [new DiskUsage(@"C:\", "", 500 * GB, 200 * GB, true)];

        public SystemMetrics Sample()
        {
            FullSamples++;
            return new SystemMetrics(37, GetMemory(), null, null, null, null, Disks);
        }

        public MemoryUsage GetMemory() => new(8 * GB, 16 * GB);

        public IReadOnlyList<DiskUsage> GetDisks() => Disks;
    }
}
