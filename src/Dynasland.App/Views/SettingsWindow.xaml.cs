using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Dynasland.App.ViewModels;

namespace Dynasland.App.Views;

public partial class SettingsWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Barra de título escura, coerente com o restante da janela.
        var hwnd = new WindowInteropHelper(this).Handle;
        var enabled = 1;
        if (DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
