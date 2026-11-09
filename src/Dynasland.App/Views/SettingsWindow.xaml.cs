using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Dynasland.App.Services;
using Dynasland.App.ViewModels;

namespace Dynasland.App.Views;

public partial class SettingsWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

    private readonly SettingsViewModel _viewModel;
    private readonly ThemeService _theme;

    public SettingsWindow(SettingsViewModel viewModel, ThemeService theme)
    {
        _viewModel = viewModel;
        _theme = theme;
        InitializeComponent();
        DataContext = viewModel;
        theme.ThemeChanged += OnThemeChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Cada seção começa do topo.
        if (e.PropertyName == nameof(SettingsViewModel.Section))
        {
            ContentScroll.ScrollToTop();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyTitleBarTheme();
    }

    protected override void OnClosed(EventArgs e)
    {
        _theme.ThemeChanged -= OnThemeChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyTitleBarTheme();

    /// <summary>Barra de título clara ou escura, coerente com o tema da janela.</summary>
    private void ApplyTitleBarTheme()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var dark = _theme.IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));
        }

        // O Windows só repinta a barra de título quando o quadro da janela é invalidado.
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, RdwFrame | RdwInvalidate | RdwUpdateNow);
    }

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint RdwInvalidate = 0x0001;
    private const uint RdwUpdateNow = 0x0100;
    private const uint RdwFrame = 0x0400;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hwnd, IntPtr updateRect, IntPtr region, uint flags);

    private async void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            "Restaurar todas as configurações do Dynasland para o padrão?\n\nAparência, animações, módulos e notificações voltam ao estado original.",
            "Restaurar configurações",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (answer == MessageBoxResult.Yes)
        {
            await _viewModel.RestoreDefaultsAsync();
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
