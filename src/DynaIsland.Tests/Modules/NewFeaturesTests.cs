using System.Text.Json;
using DynaIsland.Core.Activities;
using DynaIsland.Core.Interfaces;
using DynaIsland.Core.Models;
using DynaIsland.Core.Modules;
using DynaIsland.Core.Services;
using DynaIsland.Infrastructure.Lyrics;
using DynaIsland.Modules.Bluetooth;
using DynaIsland.Modules.Clock;
using DynaIsland.Modules.Common;
using DynaIsland.Modules.Media;
using DynaIsland.Modules.SystemMonitor;
using DynaIsland.Modules.Volume;
using Microsoft.Extensions.Time.Testing;

namespace DynaIsland.Tests.Modules;

public class NewFeaturesTests : IDisposable
{
    private readonly ActivityRecorder _recorder = new();

    public void Dispose() => _recorder.Dispose();

    // Letra sincronizada

    [Fact]
    public void Lrc_lines_are_parsed_sorted_and_stripped_of_word_tags()
    {
        var lines = LrcParser.Parse("[ar:Artista]\n[00:12.50]Segundo verso\n[00:05.00] <00:05.00>Primeiro <00:05.80>verso\n[00:20.00][01:10.00]Refrão\n[00:30.00]\nlinha sem tempo");

        Assert.Equal(["Primeiro verso", "Segundo verso", "Refrão", "", "Refrão"], lines.Select(l => l.Text));
        Assert.Equal(TimeSpan.FromSeconds(12.5), lines[1].Time);
        Assert.Equal(TimeSpan.FromSeconds(70), lines[4].Time);
    }

    [Fact]
    public void Lyric_index_follows_the_position()
    {
        var lines = LrcParser.Parse("[00:05.00]a\n[00:10.00]b\n[00:15.00]c");

        Assert.Equal(-1, LrcParser.IndexAt(lines, TimeSpan.FromSeconds(2)));
        Assert.Equal(0, LrcParser.IndexAt(lines, TimeSpan.FromSeconds(5)));
        Assert.Equal(1, LrcParser.IndexAt(lines, TimeSpan.FromSeconds(14.9)));
        Assert.Equal(2, LrcParser.IndexAt(lines, TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public void Lyrics_search_picks_the_synced_result_with_the_closest_duration()
    {
        using var document = JsonDocument.Parse("""
            [
              {"duration": 200, "syncedLyrics": null, "plainLyrics": "sem tempo"},
              {"duration": 260, "syncedLyrics": "[00:01.00]longe"},
              {"duration": 203, "syncedLyrics": "[00:01.00]perto"}
            ]
            """);

        var picked = LrcLibLyricsService.PickClosest(document.RootElement, TimeSpan.FromSeconds(201));

        Assert.Equal("perto", Assert.Single(picked!).Text);
        Assert.Null(LrcLibLyricsService.PickClosest(document.RootElement, TimeSpan.FromSeconds(100)));
    }

    [Fact]
    public async Task Lyrics_are_fetched_for_a_new_track_only_with_the_option_on()
    {
        var media = new FakeMediaService();
        var time = new FakeTimeProvider();
        var options = TestOptions.Create();
        var lyrics = new FakeLyrics();
        var module = new MediaModule(media, _recorder.Bus, new InlineDispatcher(), time, options, new FakeIslandSettings(), lyrics: lyrics);
        await module.StartAsync(CancellationToken.None);

        media.Raise(FakeMediaService.Track("Música A"));
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, lyrics.Requests);

        await options.SetAsync(MediaModule.ModuleId, MediaModule.ShowLyrics, 1);
        time.Advance(TimeSpan.FromSeconds(2));
        await lyrics.Served.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var content = Assert.IsType<MediaContent>(_recorder.PostedWithId(MediaModule.SessionActivityId("Spotify.exe")).Last().Content);
        Assert.Equal(1, lyrics.Requests);
        Assert.True(content.HasLyrics);
        Assert.Equal("primeiro", content.LyricCurrent);
        Assert.Equal("segundo", content.LyricNext);

        await module.StopAsync(CancellationToken.None);
    }

    // Volume por aplicativo

    [Fact]
    public void App_volume_rows_are_updated_in_place_and_limited()
    {
        var apps = new FakeAppVolumes();
        var content = new VolumeContent(new FakeAudio(), appVolumes: apps);
        var resized = 0;
        content.SizeChanged += (_, _) => resized++;

        content.SetApps([new("spotify", "Spotify", 0.7f, false, true), new("opera", "Opera GX", 0.35f, false, true)]);
        var spotify = content.Apps[0];
        content.SetApps([new("spotify", "Spotify", 0.5f, true, true), .. Enumerable.Range(0, 8).Select(i => new AppAudioSession($"app{i}", $"App {i}", 1, false, false))]);

        Assert.Same(spotify, content.Apps[0]);
        Assert.Equal(50, spotify.Percent);
        Assert.True(spotify.IsMuted);
        Assert.Equal(VolumeContent.MaxApps, content.Apps.Count);
        Assert.DoesNotContain(content.Apps, a => a.Id == "opera");
        Assert.Equal(0, resized); // painel fechado: ninguém precisa recalcular a altura
        Assert.Empty(apps.Levels);
    }

    [Fact]
    public void Moving_an_app_slider_changes_only_that_app()
    {
        var apps = new FakeAppVolumes();
        var content = new VolumeContent(new FakeAudio(), appVolumes: apps);
        content.SetApps([new("spotify", "Spotify", 0.7f, true, true)]);

        content.Apps[0].Level = 40;

        Assert.Equal(("spotify", 0.4f), Assert.Single(apps.Levels));
        Assert.Equal(("spotify", false), Assert.Single(apps.Mutes));
        Assert.False(content.Apps[0].IsMuted);
    }

    [Fact]
    public void Volume_panel_grows_with_the_app_list()
    {
        var content = new VolumeContent(new FakeAudio(), appVolumes: new FakeAppVolumes());
        var before = content.GetPreferredDimensions(IslandState.Interactive)!.Height;

        content.SetApps([new("spotify", "Spotify", 0.7f, false, true), new("opera", "Opera GX", 0.35f, false, true)]);

        Assert.Equal(before + 30 + 2 * 32, content.GetPreferredDimensions(IslandState.Interactive)!.Height);
    }

    // Rede

    [Fact]
    public void Network_rate_is_formatted_per_second()
    {
        Assert.Equal("12,4 MB/s", TextFormat.Rate(12.4 * 1024 * 1024));
        Assert.Equal("0 B/s", TextFormat.Rate(0));

        var content = new SystemMonitorContent(_ => { });
        var metrics = new SystemMetrics(10, new MemoryUsage(1, 2), null, null, null, null, []);
        content.Apply(metrics with { Network = new NetworkRate(2048, 512) });
        content.Apply(metrics);

        Assert.Equal("2 KB/s", content.NetworkDownText);
        Assert.Equal("512 B/s", content.NetworkUpText);
    }

    [Fact]
    public void Idle_network_glance_stays_out_of_the_home_list()
    {
        var content = new ClockContent();
        IslandGlance[] glances = [new(SystemMonitorModule.NetworkGlanceId, "", "↓ 1 MB/s  ↑ 2 KB/s", IdleOnly: true)];

        content.SetGlances(glances);
        content.SetIdle(IdleDisplay.Network, glances);

        Assert.Empty(content.Glances);
        Assert.Equal("↓ 1 MB/s  ↑ 2 KB/s", content.IdleText);
    }

    // Bluetooth

    [Fact]
    public async Task Bluetooth_announces_connection_with_battery_once_per_device()
    {
        var bluetooth = new FakeBluetooth { Battery = 80 };
        var time = new FakeTimeProvider();
        var module = new BluetoothModule(bluetooth, _recorder.Bus, time, TestOptions.Create());
        await module.StartAsync(CancellationToken.None);

        // O mesmo fone aparece como dois dispositivos (clássico e de baixa energia).
        bluetooth.Raise(new BluetoothDeviceInfo("classic", "Fuxi-H6", true));
        bluetooth.Raise(new BluetoothDeviceInfo("le", "Fuxi-H6", true));
        time.Advance(TimeSpan.FromSeconds(3));
        await bluetooth.BatteryRead.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Yield();

        var posted = Assert.Single(_recorder.Posted);
        var status = Assert.IsType<StatusContent>(posted.Content);
        Assert.Equal("Fuxi-H6", status.Title);
        Assert.Equal("Conectado · bateria 80%", status.Message);

        time.Advance(TimeSpan.FromSeconds(10));
        bluetooth.Raise(new BluetoothDeviceInfo("classic", "Fuxi-H6", false));
        Assert.Equal("Desconectado", Assert.IsType<StatusContent>(_recorder.Posted.Last().Content).Message);

        await module.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(4u, 0u, null, null, "Fuxi-H6", BluetoothDeviceKind.Headset)]
    [InlineData(5u, 0x20u, null, null, "X", BluetoothDeviceKind.Mouse)]
    [InlineData(5u, 0x10u, null, null, "X", BluetoothDeviceKind.Keyboard)]
    [InlineData(5u, 0x02u, null, null, "X", BluetoothDeviceKind.Gamepad)]
    [InlineData(null, null, 15u, 2u, "X", BluetoothDeviceKind.Mouse)]
    [InlineData(null, null, 15u, 1u, "X", BluetoothDeviceKind.Keyboard)]
    [InlineData(null, null, null, null, "MX Master 3", BluetoothDeviceKind.Mouse)]
    [InlineData(null, null, null, null, "Caixa de som", BluetoothDeviceKind.Other)]
    public void Bluetooth_device_kind_is_classified(uint? major, uint? minor, uint? category, uint? sub, string name, BluetoothDeviceKind expected) =>
        Assert.Equal(expected, BluetoothDeviceClassifier.Classify(major, minor, category, sub, name));

    [Fact]
    public async Task Bluetooth_panel_lists_connected_devices_with_battery()
    {
        var bluetooth = new FakeBluetooth
        {
            Battery = 55,
            Connected = [new("a", "Mouse BLE", true, BluetoothDeviceKind.Mouse), new("b", "Fone", false, BluetoothDeviceKind.Headset)],
        };
        var shortcuts = new ShortcutRegistry();
        var module = new BluetoothModule(bluetooth, _recorder.Bus, new FakeTimeProvider(), TestOptions.Create(), shortcuts, new InlineDispatcher());
        await module.StartAsync(CancellationToken.None);

        module.OpenPanel();
        var panel = Assert.IsType<BluetoothDevicesContent>(Assert.Single(_recorder.Posted).Content);
        panel.OnPresentationChanged(IslandState.Interactive);
        await bluetooth.BatteryRead.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);

        var row = Assert.Single(panel.Devices);
        Assert.Equal("Mouse BLE", row.Name);
        Assert.Equal("55%", row.BatteryText);

        panel.OnPresentationChanged(null);
        await module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Bluetooth_module_is_unavailable_without_an_adapter()
    {
        var module = new BluetoothModule(new FakeBluetooth { HasAdapter = false }, _recorder.Bus, new FakeTimeProvider(), TestOptions.Create());

        await Assert.ThrowsAsync<ModuleUnavailableException>(() => module.StartAsync(CancellationToken.None));
    }

    private sealed class FakeLyrics : ILyricsService
    {
        public int Requests { get; private set; }

        public TaskCompletionSource Served { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<LyricLine>?> GetAsync(string title, string artist, string? album, TimeSpan? duration, CancellationToken cancellationToken = default)
        {
            Requests++;
            Served.TrySetResult();
            // Track() começa aos 30 s: o primeiro verso já está em andamento.
            return Task.FromResult<IReadOnlyList<LyricLine>?>(LrcParser.Parse("[00:10.00]primeiro\n[05:00.00]segundo"));
        }
    }

    private sealed class FakeAppVolumes : IAppVolumeService
    {
        public List<(string Id, float Level)> Levels { get; } = [];

        public List<(string Id, bool Muted)> Mutes { get; } = [];

        public Task<IReadOnlyList<AppAudioSession>> GetSessionsAsync() => Task.FromResult<IReadOnlyList<AppAudioSession>>([]);

        public Task SetLevelAsync(string id, float level)
        {
            Levels.Add((id, level));
            return Task.CompletedTask;
        }

        public Task SetMutedAsync(string id, bool muted)
        {
            Mutes.Add((id, muted));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAudio : IAudioVolumeService
    {
        public VolumeState? Current => null;

        public event EventHandler<VolumeChangedEventArgs>? Changed
        {
            add { }
            remove { }
        }

        public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task StopAsync() => Task.CompletedTask;

        public void SetLevel(float level)
        {
        }

        public void SetMuted(bool muted)
        {
        }
    }

    private sealed class FakeBluetooth : IBluetoothService
    {
        public bool HasAdapter { get; init; } = true;

        public int? Battery { get; init; }

        public TaskCompletionSource BatteryRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event EventHandler<BluetoothDeviceInfo>? ConnectionChanged;

        public IReadOnlyList<BluetoothDeviceInfo> Connected { get; init; } = [];

        public IReadOnlyList<BluetoothDeviceInfo> Devices => Connected;

        public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(HasAdapter);

        public Task StopAsync() => Task.CompletedTask;

        public Task<int?> GetBatteryAsync(string deviceId)
        {
            BatteryRead.TrySetResult();
            return Task.FromResult(Battery);
        }

        public void Raise(BluetoothDeviceInfo device) => ConnectionChanged?.Invoke(this, device);
    }
}
