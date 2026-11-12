using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Services;
using Dynasland.Modules.Common;

namespace Dynasland.Tests.Modules;

public class DockBadgesTests
{
    private static IslandShortcut Shortcut(string module) => new(module + ".open", module, "", module, () => { });

    [Fact]
    public void Registry_sets_clears_and_only_notifies_on_real_changes()
    {
        var registry = new DockBadgeRegistry();
        var notifications = 0;
        registry.Changed += (_, _) => notifications++;

        registry.Set("water", new DockBadge(DockBadgeKind.Ring, 0.25));
        registry.Set("water", new DockBadge(DockBadgeKind.Ring, 0.25));
        registry.Set("water", new DockBadge(DockBadgeKind.Ring, 0.5));
        registry.Clear("water");
        registry.Clear("water");

        Assert.Equal(3, notifications);
        Assert.Empty(registry.Badges);
    }

    [Fact]
    public void Registry_clamps_the_progress_between_zero_and_one()
    {
        var registry = new DockBadgeRegistry();

        registry.Set("water", new DockBadge(DockBadgeKind.Ring, 3));

        Assert.Equal(1, registry.Badges["water"].Progress);
    }

    [Fact]
    public void Items_carry_the_badge_of_their_module()
    {
        var badges = new Dictionary<string, DockBadge> { ["water"] = new(DockBadgeKind.Ring, 0.5) };

        var items = DockItems.Build([Shortcut("volume"), Shortcut("water")], [], null, badges: badges);

        Assert.Null(items.Single(i => i.ModuleId == "volume").Badge);
        Assert.Equal(new DockBadge(DockBadgeKind.Ring, 0.5), items.Single(i => i.ModuleId == "water").Badge);
    }

    [Fact]
    public void Hidden_modules_leave_the_dock_unless_they_have_an_activity()
    {
        var hidden = DockVisibility.Parse("weather,media");

        var items = DockItems.Build(
            [Shortcut("weather"), Shortcut("volume"), Shortcut("media")],
            [new DockActivity("m1", "media")],
            null,
            hidden);

        Assert.Equal(["media", "volume"], items.Select(i => i.ModuleId));
    }

    [Theory]
    [InlineData(null, "weather", false, "weather")]
    [InlineData("weather", "weather", false, "weather")]
    [InlineData("weather", "weather", true, "")]
    [InlineData("water,weather", "water", true, "weather")]
    [InlineData("", "weather", true, "")]
    public void Visibility_toggles_a_module_in_the_list(string? current, string moduleId, bool visible, string expected)
    {
        Assert.Equal(expected, DockVisibility.With(current, moduleId, visible));
    }

    [Fact]
    public void Visibility_list_is_sorted_and_free_of_repeats()
    {
        var result = DockVisibility.With("weather,media", "water", visible: false);

        Assert.Equal("media,water,weather", result);
        Assert.Equal(3, DockVisibility.Parse(result).Count);
    }

    [Theory]
    [InlineData(IslandState.Compact, "clock", "clock", null)]
    [InlineData(IslandState.Compact, "media", "media", "media")]
    [InlineData(IslandState.Compact, null, null, null)]
    [InlineData(IslandState.Interactive, "clock", "weather", "weather")]
    [InlineData(IslandState.Expanded, "media", "notifications", "notifications")]
    public void Only_a_real_activity_is_highlighted_at_rest(IslandState state, string? indicator, string? focused, string? expected)
    {
        Assert.Equal(expected, DockItems.FocusedModule(state, indicator, focused));
    }

    [Fact]
    public void Hidden_modules_round_trip_through_the_settings_mapper()
    {
        var values = IslandSettingsMapper.ToValues(IslandSettings.Default with { DockHiddenModules = "media,weather" });

        Assert.Equal("media,weather", IslandSettingsMapper.FromValues(values).DockHiddenModules);
        Assert.Equal(string.Empty, IslandSettingsMapper.FromValues(new Dictionary<string, string>()).DockHiddenModules);
    }
}
