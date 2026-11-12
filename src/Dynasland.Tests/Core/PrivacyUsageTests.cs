using Dynasland.Core.Models;
using Dynasland.Core.Services;

namespace Dynasland.Tests.Core;

public class PrivacyUsageTests
{
    private const long Start = 134388775624312901;
    private const long Stop = 134388776857394883;

    [Fact]
    public void Started_and_not_stopped_is_in_use() => Assert.True(PrivacyUsageEvaluator.IsInUse(Start, 0));

    [Fact]
    public void Stopped_after_start_is_not_in_use() => Assert.False(PrivacyUsageEvaluator.IsInUse(Start, Stop));

    [Fact]
    public void Never_used_is_not_in_use() => Assert.False(PrivacyUsageEvaluator.IsInUse(0, 0));

    [Fact]
    public void Negative_values_are_ignored() => Assert.False(PrivacyUsageEvaluator.IsInUse(-1, 0));

    [Fact]
    public void No_entries_means_nothing_in_use()
    {
        var usage = PrivacyUsageEvaluator.Evaluate([]);

        Assert.Equal(PrivacyUsage.None, usage);
        Assert.False(usage.Microphone);
        Assert.False(usage.Camera);
        Assert.Empty(usage.MicrophoneApps);
        Assert.Empty(usage.CameraApps);
    }

    [Fact]
    public void Separates_microphone_and_camera_and_lists_only_active_apps()
    {
        var usage = PrivacyUsageEvaluator.Evaluate(
        [
            new PrivacyUsageEntry(PrivacyCapability.Microphone, "C:#Program Files#BraveSoftware#Brave-Browser#Application#brave.exe", Start, 0),
            new PrivacyUsageEntry(PrivacyCapability.Microphone, "C:#Program Files#obs-studio#bin#64bit#obs64.exe", Start, Stop),
            new PrivacyUsageEntry(PrivacyCapability.Microphone, "5319275A.WhatsAppDesktop_cv1g1gvanyjgm", Start, 0),
            new PrivacyUsageEntry(PrivacyCapability.Camera, "Microsoft.WindowsCamera_8wekyb3d8bbwe", 0, 0),
        ]);

        Assert.True(usage.Microphone);
        Assert.False(usage.Camera);
        Assert.Equal(["brave.exe", "WhatsAppDesktop"], usage.MicrophoneApps);
        Assert.Empty(usage.CameraApps);
    }

    [Fact]
    public void Same_program_in_several_keys_is_listed_once()
    {
        var usage = PrivacyUsageEvaluator.Evaluate(
        [
            new PrivacyUsageEntry(PrivacyCapability.Camera, "C:#Program Files (x86)#Microsoft#EdgeWebView#Application#155.0#msedgewebview2.exe", Start, 0),
            new PrivacyUsageEntry(PrivacyCapability.Camera, "C:#Program Files (x86)#Microsoft#EdgeWebView#Application#156.0#msedgewebview2.exe", Start, 0),
        ]);

        Assert.True(usage.Camera);
        Assert.Equal(["msedgewebview2.exe"], usage.CameraApps);
    }

    [Theory]
    [InlineData("C:#Program Files#BraveSoftware#Brave-Browser#Application#brave.exe", "brave.exe")]
    [InlineData("C:#Users#USER#AppData#Local#Programs#Opera GX#opera.exe", "opera.exe")]
    [InlineData("5319275A.WhatsAppDesktop_cv1g1gvanyjgm", "WhatsAppDesktop")]
    [InlineData("Microsoft.Office.OneNote_8wekyb3d8bbwe", "OneNote")]
    [InlineData("SemPonto_abc123", "SemPonto")]
    [InlineData("Simples", "Simples")]
    public void Decodes_readable_app_names(string key, string expected) =>
        Assert.Equal(expected, PrivacyUsageEvaluator.AppNameFromKey(key));

    [Fact]
    public void Privacy_indicators_are_on_by_default()
    {
        Assert.True(IslandSettings.Default.ShowPrivacyIndicators);
        Assert.True(IslandSettingsMapper.FromValues(new Dictionary<string, string>()).ShowPrivacyIndicators);
    }

    [Fact]
    public void Privacy_indicators_setting_round_trips()
    {
        var off = IslandSettings.Default with { ShowPrivacyIndicators = false };

        var values = IslandSettingsMapper.ToValues(off);

        Assert.Equal("false", values[IslandSettingsMapper.Keys.ShowPrivacyIndicators]);
        Assert.False(IslandSettingsMapper.FromValues(values).ShowPrivacyIndicators);
    }

    [Theory]
    [InlineData("talvez")]
    [InlineData("")]
    [InlineData("0")]
    public void Invalid_privacy_indicators_value_falls_back_to_default(string raw)
    {
        var values = new Dictionary<string, string> { [IslandSettingsMapper.Keys.ShowPrivacyIndicators] = raw };

        Assert.True(IslandSettingsMapper.FromValues(values).ShowPrivacyIndicators);
    }
}
