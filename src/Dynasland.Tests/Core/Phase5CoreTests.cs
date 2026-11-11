using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Services;
using Dynasland.Tests.Modules;

namespace Dynasland.Tests.Core;

public class Phase5CoreTests
{
    [Fact]
    public void All_settings_round_trip_through_storage()
    {
        var custom = IslandSettings.Default with
        {
            Theme = AppTheme.Light,
            IslandTheme = IslandTheme.Aurora,
            AccentColor = "#0A84FF",
            IslandOpacity = 0.8,
            ShadowIntensity = 0.2,
            Roundness = 0.6,
            CompactWidth = 220,
            CompactHeight = 38,
            MaxWidth = 380,
            AnimationIntensity = 1.5,
            StartHidden = true,
            HideInFullscreen = false,
            CheckForUpdates = false,
        };

        var restored = IslandSettingsMapper.FromValues(IslandSettingsMapper.ToValues(custom));

        Assert.Equal(custom, restored);
    }

    [Fact]
    public void Invalid_theme_and_accent_fall_back_to_defaults()
    {
        var values = new Dictionary<string, string>
        {
            [IslandSettingsMapper.Keys.Theme] = "Neon",
            [IslandSettingsMapper.Keys.IslandTheme] = "Neon",
            [IslandSettingsMapper.Keys.AccentColor] = "verde",
            [IslandSettingsMapper.Keys.MaxWidth] = "9999",
        };

        var settings = IslandSettingsMapper.FromValues(values);

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(IslandTheme.Black, settings.IslandTheme);
        Assert.Equal(IslandSettings.Default.AccentColor, settings.AccentColor);
        Assert.Equal(420, settings.MaxWidth);
    }

    [Fact]
    public void Appearance_sets_compact_size_and_keeps_pill_shape()
    {
        var settings = IslandSettings.Default with { CompactWidth = 220, CompactHeight = 40 };

        var compact = IslandAppearance.Adjust(IslandLayout.Compact, IslandState.Compact, settings);

        Assert.Equal(new IslandDimensions(220, 40, 20), compact);
    }

    [Fact]
    public void Appearance_keeps_extra_width_requested_by_indicators()
    {
        var mediaIndicator = new IslandDimensions(232, 34, 17);

        var compact = IslandAppearance.Adjust(mediaIndicator, IslandState.Compact, IslandSettings.Default with { CompactWidth = 200 });

        Assert.Equal(242, compact.Width);
    }

    [Fact]
    public void Appearance_caps_width_and_scales_roundness()
    {
        var settings = IslandSettings.Default with { MaxWidth = 360, Roundness = 0.5 };

        var interactive = IslandAppearance.Adjust(IslandLayout.Interactive, IslandState.Interactive, settings);

        Assert.Equal(360, interactive.Width);
        Assert.Equal(IslandLayout.Interactive.Height, interactive.Height);
        Assert.Equal(IslandLayout.Interactive.CornerRadius / 2, interactive.CornerRadius);
    }

    [Fact]
    public async Task Module_options_default_persist_and_normalize()
    {
        var store = new InMemorySettingsStore();
        var options = new ModuleOptionsService(store);
        var toggle = ModuleOption.Toggle("x", "X", "");
        var slider = ModuleOption.Slider("y", "Y", "", 20, 10, 40, 5, "%");
        (string, string)? changed = null;
        options.Changed += (_, e) => changed = e;

        Assert.True(options.IsOn("m", toggle));
        Assert.Equal(20, options.Get("m", slider));

        await options.SetAsync("m", toggle, 0);
        await options.SetAsync("m", slider, 33);

        Assert.False(options.IsOn("m", toggle));
        Assert.Equal(35, options.Get("m", slider));
        Assert.Equal(("m", "y"), changed);

        var reloaded = new ModuleOptionsService(store);
        await reloaded.LoadAsync();
        Assert.False(reloaded.IsOn("m", toggle));
        Assert.Equal(35, reloaded.Get("m", slider));
        Assert.Equal("35", store.Values["module.m.y"]);
    }

    [Fact]
    public async Task Settings_reset_restores_defaults_and_notifies()
    {
        var store = new InMemorySettingsStore();
        using var service = new IslandSettingsService(store);
        await service.UpdateAsync(s => s with { Theme = AppTheme.Dark, CompactWidth = 250 });
        IslandSettings? raised = null;
        service.Changed += (_, s) => raised = s;

        await service.ResetAsync();

        Assert.Equal(IslandSettings.Default, service.Current);
        Assert.Equal(IslandSettings.Default, raised);
        Assert.Empty(store.Values);
    }

    [Theory]
    [InlineData("v0.2.0", "0.1.0", true)]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("v0.1", "0.1.0", false)]
    [InlineData("v1.0.0-beta.2", "0.9.9", true)]
    [InlineData("v0.0.9", "0.1.0", false)]
    [InlineData("nao-e-versao", "0.1.0", false)]
    public void Release_tags_are_compared_with_current_version(string tag, string current, bool expected)
    {
        Assert.Equal(expected, ReleaseVersion.IsNewer(tag, Version.Parse(current)));
    }

    [Fact]
    public void Update_status_texts_are_clear()
    {
        Assert.Equal("Nova versão disponível: 0.2.0.",
            App.ViewModels.SettingsViewModel.Describe(new UpdateCheckResult(UpdateStatus.UpdateAvailable, "0.2.0", "https://github.com/x")));
        Assert.Equal("Nenhuma versão publicada ainda.",
            App.ViewModels.SettingsViewModel.Describe(new UpdateCheckResult(UpdateStatus.NoReleases, null, null)));
    }
}
