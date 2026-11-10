using Dynasland.Core.Activities;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Shortcuts;
using Dynasland.Modules.Brightness;
using Dynasland.Modules.Common;
using Dynasland.Modules.Volume;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class VolumeAndBrightnessModuleTests : IDisposable
{
    private readonly ActivityRecorder _recorder = new();
    private readonly InlineDispatcher _ui = new();

    public void Dispose() => _recorder.Dispose();

    [Fact]
    public async Task External_volume_change_shows_high_priority_transient()
    {
        var audio = new FakeAudioService();
        var module = new VolumeModule(audio, new FakeAudioDevices(), _recorder.Bus, _ui, TestOptions.Create(), new GlobalShortcutRegistry(new InMemorySettingsStore()));
        await module.StartAsync(CancellationToken.None);

        audio.Raise(new VolumeState(0.8f, false, "Fones"), VolumeChangeReason.External);

        var activity = Assert.Single(_recorder.Posted);
        Assert.Equal(VolumeModule.ActivityId, activity.Id);
        Assert.Equal(ActivityPriority.High, activity.Priority);
        var content = Assert.IsType<VolumeContent>(activity.Content);
        Assert.Equal(80, content.Percent);
        Assert.Equal("Fones", content.DeviceName);
        Assert.Empty(audio.LevelsSet);
    }

    [Fact]
    public async Task Own_volume_change_updates_content_without_announcing()
    {
        var audio = new FakeAudioService();
        var module = new VolumeModule(audio, new FakeAudioDevices(), _recorder.Bus, _ui, TestOptions.Create(), new GlobalShortcutRegistry(new InMemorySettingsStore()));
        await module.StartAsync(CancellationToken.None);

        audio.Raise(new VolumeState(0.3f, false, null), VolumeChangeReason.Own);

        Assert.Empty(_recorder.Posted);
    }

    [Fact]
    public async Task Slider_sets_volume_and_unmutes()
    {
        var audio = new FakeAudioService();
        var module = new VolumeModule(audio, new FakeAudioDevices(), _recorder.Bus, _ui, TestOptions.Create(), new GlobalShortcutRegistry(new InMemorySettingsStore()));
        await module.StartAsync(CancellationToken.None);
        audio.Raise(new VolumeState(0.5f, true, null), VolumeChangeReason.External);
        var content = Assert.IsType<VolumeContent>(_recorder.Posted.Single().Content);

        content.Level = 25;

        Assert.Equal([0.25f], audio.LevelsSet);
        Assert.Equal([false], audio.MuteSet);

        content.ToggleMuteCommand.Execute(null);
        Assert.Equal([false, false], audio.MuteSet);
    }

    [Fact]
    public async Task Volume_is_unavailable_without_output_device()
    {
        var module = new VolumeModule(new FakeAudioService { HasDevice = false }, new FakeAudioDevices(), _recorder.Bus, _ui, TestOptions.Create(), new GlobalShortcutRegistry(new InMemorySettingsStore()));

        await Assert.ThrowsAsync<ModuleUnavailableException>(() => module.StartAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(false, 0, Glyphs.Mute)]
    [InlineData(true, 80, Glyphs.Mute)]
    [InlineData(false, 20, Glyphs.VolumeLow)]
    [InlineData(false, 50, Glyphs.VolumeMedium)]
    [InlineData(false, 90, Glyphs.VolumeHigh)]
    public void Volume_glyph_reflects_level_and_mute(bool muted, double level, string expected)
    {
        Assert.Equal(expected, VolumeContent.GlyphFor(muted, level));
    }

    [Fact]
    public async Task Brightness_is_unavailable_when_hardware_has_no_control()
    {
        var module = new BrightnessModule(new FakeBrightnessService { Supported = false }, new FakeExternalBrightness(), _recorder.Bus, _ui, new FakeTimeProvider(), TestOptions.Create(), new ShortcutRegistry(), new GlobalShortcutRegistry(new InMemorySettingsStore()));

        var error = await Assert.ThrowsAsync<ModuleUnavailableException>(() => module.StartAsync(CancellationToken.None));
        Assert.Contains("brilho", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task External_brightness_change_is_announced_but_echo_of_own_change_is_not()
    {
        var time = new FakeTimeProvider();
        var brightness = new FakeBrightnessService();
        var module = new BrightnessModule(brightness, new FakeExternalBrightness(), _recorder.Bus, _ui, time, TestOptions.Create(), new ShortcutRegistry(), new GlobalShortcutRegistry(new InMemorySettingsStore()));
        await module.StartAsync(CancellationToken.None);

        brightness.Raise(70);
        var content = Assert.IsType<BrightnessContent>(Assert.Single(_recorder.Posted).Content);
        Assert.Equal(70, content.Percent);

        content.Level = 40;
        await WaitUntilAsync(() => brightness.Set.Contains(40));
        brightness.Raise(40);
        Assert.Single(_recorder.Posted);

        time.Advance(TimeSpan.FromSeconds(5));
        brightness.Raise(55);
        Assert.Equal(2, _recorder.Posted.Count);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}

public class HardwareControlTests : IDisposable
{
    private readonly ActivityRecorder _recorder = new();
    private readonly InlineDispatcher _ui = new();

    public void Dispose() => _recorder.Dispose();

    [Fact]
    public async Task External_monitors_make_brightness_available_and_panel_controls_each_one()
    {
        var external = new FakeExternalBrightness();
        external.Displays.Add(new ExternalDisplayBrightness(@"\.\DISPLAY1#0", "Monitor 1", 90));
        external.Displays.Add(new ExternalDisplayBrightness(@"\.\DISPLAY2#0", "Monitor 2", 56));
        var module = new BrightnessModule(new FakeBrightnessService { Supported = false }, external, _recorder.Bus, _ui, new FakeTimeProvider(),
            TestOptions.Create(), new ShortcutRegistry(), new GlobalShortcutRegistry(new InMemorySettingsStore()));

        await module.StartAsync(CancellationToken.None);
        module.OpenPanel();

        var panel = Assert.IsType<BrightnessPanelContent>(Assert.Single(_recorder.Posted).Content);
        Assert.Equal(["Monitor 1", "Monitor 2"], panel.Displays.Select(d => d.Name));
        Assert.Equal(56, panel.Displays[1].Percent);

        panel.Displays[1].Level = 30;
        await WaitUntilAsync(() => external.Set.Contains((@"\.\DISPLAY2#0", 30)));
    }

    [Fact]
    public async Task Volume_panel_lists_outputs_switches_default_and_controls_microphone()
    {
        var devices = new FakeAudioDevices { Microphone = new MicrophoneState(0.5f, false, "Microfone USB") };
        devices.Outputs.Add(new AudioDevice("a", "Alto-falantes", true));
        devices.Outputs.Add(new AudioDevice("b", "Fone", false));
        var module = new VolumeModule(new FakeAudioService(), devices, _recorder.Bus, _ui, TestOptions.Create(), new GlobalShortcutRegistry(new InMemorySettingsStore()));
        await module.StartAsync(CancellationToken.None);

        var content = (VolumeContent)typeof(VolumeModule).GetField("_content", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(module)!;

        Assert.True(content.HasDeviceChoice);
        Assert.True(content.HasMicrophone);
        Assert.Equal(50, content.MicrophonePercent);

        content.Outputs[1].Select.Execute(null);
        await WaitUntilAsync(() => devices.DefaultsSet.Contains("b"));

        content.ToggleMicrophoneMuteCommand.Execute(null);
        Assert.True(content.MicrophoneMuted);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}
