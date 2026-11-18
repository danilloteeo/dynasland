using DynaIsland.Core.Models;
using DynaIsland.Core.Services;

namespace DynaIsland.Tests.Core;

public class VidroSettingsTests
{
    private static IslandSettings Read(params (string Key, string Value)[] values) =>
        IslandSettingsMapper.FromValues(values.ToDictionary(v => v.Key, v => v.Value));

    [Fact]
    public void Defaults_are_ice_blue_and_70_percent_glass()
    {
        Assert.Equal("#9CC2FF", IslandSettings.Default.AccentColor);
        Assert.Equal(0.70, IslandSettings.Default.IslandOpacity);
        Assert.False(IslandSettings.Default.AccentFollowsWindows);
    }

    [Fact]
    public void Old_white_default_accent_migrates_to_ice_blue()
    {
        Assert.Equal("#9CC2FF", Read((IslandSettingsMapper.Keys.AccentColor, "#ffffff")).AccentColor);
    }

    [Fact]
    public void Custom_accent_is_kept()
    {
        Assert.Equal("#0A84FF", Read((IslandSettingsMapper.Keys.AccentColor, "#0a84ff")).AccentColor);
    }

    [Fact]
    public void Opacity_saved_before_the_glass_is_ignored()
    {
        // Até a 1.8.x a cápsula era sempre opaca (1,0) e a opção não aparecia: começa no novo padrão.
        Assert.Equal(0.70, Read(("appearance.opacity", "1")).IslandOpacity);
    }

    [Fact]
    public void Glass_opacity_is_clamped_between_half_and_full()
    {
        Assert.Equal(0.5, Read((IslandSettingsMapper.Keys.IslandOpacity, "0.2")).IslandOpacity);
        Assert.Equal(0.85, Read((IslandSettingsMapper.Keys.IslandOpacity, "0.85")).IslandOpacity);
    }

    [Fact]
    public void Follow_windows_accent_round_trips()
    {
        var values = IslandSettingsMapper.ToValues(IslandSettings.Default with { AccentFollowsWindows = true });

        Assert.True(IslandSettingsMapper.FromValues(values).AccentFollowsWindows);
    }
}
