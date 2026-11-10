using Dynasland.Core.Shortcuts;
using Dynasland.Tests.Modules;

namespace Dynasland.Tests.Core;

public class GlobalShortcutTests
{
    [Theory]
    [InlineData("Ctrl+Alt+D", HotkeyModifiers.Control | HotkeyModifiers.Alt, 'D')]
    [InlineData("ctrl + shift + F5", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x74)]
    [InlineData("Win+Espaço", HotkeyModifiers.Windows, 0x20)]
    [InlineData("Alt+0x2C", HotkeyModifiers.Alt, 0x2C)]
    public void Parses_gestures(string text, HotkeyModifiers modifiers, int key)
    {
        Assert.Equal(new HotkeyGesture(modifiers, key), HotkeyGesture.TryParse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("D")]
    [InlineData("Shift+D")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Nada")]
    public void Rejects_invalid_gestures(string text) => Assert.Null(HotkeyGesture.TryParse(text));

    [Fact]
    public void Formats_round_trip()
    {
        var gesture = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 'S');
        Assert.Equal("Ctrl+Alt+Shift+S", gesture.ToString());
        Assert.Equal(gesture, HotkeyGesture.TryParse(gesture.ToString()));
    }

    [Fact]
    public async Task Custom_gesture_is_persisted_and_taken_from_other_action()
    {
        var store = new InMemorySettingsStore();
        var registry = new GlobalShortcutRegistry(store);
        var ctrlAltD = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'D');
        registry.Register(new GlobalShortcut("a", "A", "x", ctrlAltD, () => { }));
        registry.Register(new GlobalShortcut("b", "B", "x", null, () => { }));

        await registry.SetGestureAsync("b", ctrlAltD);

        Assert.Null(registry.Bindings.Single(b => b.Shortcut.Id == "a").Gesture);
        Assert.Equal(ctrlAltD, registry.Bindings.Single(b => b.Shortcut.Id == "b").Gesture);

        var reloaded = new GlobalShortcutRegistry(store);
        await reloaded.LoadAsync();
        reloaded.Register(new GlobalShortcut("a", "A", "x", ctrlAltD, () => { }));
        Assert.Null(reloaded.Bindings.Single().Gesture);

        await reloaded.ResetAsync("a");
        Assert.Equal(ctrlAltD, reloaded.Bindings.Single().Gesture);
        Assert.False(reloaded.Bindings.Single().IsCustom);
    }

    [Fact]
    public void Unregistered_action_disappears()
    {
        var registry = new GlobalShortcutRegistry(new InMemorySettingsStore());
        var registration = registry.Register(new GlobalShortcut("a", "A", "x", null, () => { }));

        registration.Dispose();

        Assert.Empty(registry.Bindings);
    }
}
