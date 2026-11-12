using Dynasland.Core.Modules;
using Dynasland.Modules.Common;

namespace Dynasland.Tests.Modules;

public class DockItemsTests
{
    private static IslandShortcut Shortcut(string id, string module) => new(id, module, "", id, () => { });

    [Fact]
    public void One_item_per_module_even_with_several_shortcuts_and_activities()
    {
        var items = DockItems.Build(
            [Shortcut("volume.a", "volume"), Shortcut("volume.b", "volume")],
            [new DockActivity("a1", "volume")],
            focusedModuleId: null);

        Assert.Single(items);
        Assert.Equal("volume", items[0].ModuleId);
    }

    [Fact]
    public void An_activity_wins_over_a_shortcut_of_the_same_module()
    {
        var items = DockItems.Build([Shortcut("media.open", "media")], [new DockActivity("m1", "media")], null);

        Assert.Equal(DockItemKind.Activity, items[0].Kind);
        Assert.Equal("m1", items[0].ActivityId);
        Assert.Null(items[0].ShortcutId);
    }

    [Fact]
    public void Shortcut_only_modules_open_through_the_shortcut()
    {
        var items = DockItems.Build([Shortcut("weather.panel", "weather")], [], null);

        Assert.Equal(DockItemKind.Shortcut, items[0].Kind);
        Assert.Equal("weather.panel", items[0].ShortcutId);
        Assert.Null(items[0].ActivityId);
    }

    [Fact]
    public void Items_follow_the_fixed_module_order_not_the_input_order()
    {
        var items = DockItems.Build(
            [Shortcut("w", "weather"), Shortcut("v", "volume")],
            [new DockActivity("m1", "media")],
            null);

        Assert.Equal(["media", "volume", "weather"], items.Select(i => i.ModuleId));
    }

    [Fact]
    public void The_focused_module_is_marked_active()
    {
        var items = DockItems.Build([Shortcut("v", "volume"), Shortcut("w", "weather")], [], focusedModuleId: "weather");

        Assert.Equal([false, true], items.Select(i => i.IsActive));
    }

    [Fact]
    public void Unknown_modules_and_the_app_module_are_left_out()
    {
        var items = DockItems.Build([Shortcut("x", "desconhecido"), Shortcut("app.settings", PageIcons.AppModuleId)], [], null);

        Assert.Empty(items);
    }

    [Fact]
    public void Every_item_has_a_vector_glyph_and_a_label()
    {
        var items = DockItems.Build([Shortcut("v", "volume")], [], null);

        Assert.NotNull(VectorIcons.Get(items[0].Glyph));
        Assert.False(string.IsNullOrWhiteSpace(items[0].Label));
    }
}
