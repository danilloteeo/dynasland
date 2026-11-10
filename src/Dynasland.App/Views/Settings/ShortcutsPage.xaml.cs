using System.Windows.Controls;
using System.Windows.Input;
using Dynasland.App.ViewModels;
using Dynasland.Core.Shortcuts;

namespace Dynasland.App.Views.Settings;

public partial class ShortcutsPage : UserControl
{
    public ShortcutsPage()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Durante a gravação, a próxima combinação pressionada vira o novo atalho.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel { Shortcuts.IsRecording: true } viewModel)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        e.Handled = viewModel.Shortcuts.OnKey(KeyInterop.VirtualKeyFromKey(key), modifiers);
    }
}
