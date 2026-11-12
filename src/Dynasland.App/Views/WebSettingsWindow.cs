using System.IO;
using System.Windows;
using System.Windows.Media;
using Dynasland.App.Services;
using Dynasland.App.ViewModels;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Dynasland.App.Views;

/// <summary>
/// Janela de configurações: uma página web (Web/settings.html, com o visual do Volum) dentro do WebView2.
/// A página conversa com o <see cref="SettingsViewModel"/> pela <see cref="SettingsBridge"/>.
/// </summary>
public sealed class WebSettingsWindow : Window
{
    private const string Host = "dynasland.local";

    private readonly WebView2 _web = new();
    private SettingsBridge? _bridge;

    public WebSettingsWindow(SettingsViewModel viewModel, ThemeService theme)
    {
        ViewModel = viewModel;
        Title = "Configurações do Dynasland";
        Width = 920;
        Height = 680;
        MinWidth = 760;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x09, 0x09, 0x09));
        UseLayoutRounding = true;
        Content = _web;
        _web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0x09, 0x09, 0x09);

        TitleBarTheme.Attach(this, theme);
        Loaded += async (_, _) => await InitializeWebAsync();
    }

    public SettingsViewModel ViewModel { get; }

    protected override void OnClosed(EventArgs e)
    {
        _bridge?.Dispose();
        ViewModel.Dispose();
        _web.Dispose();
        base.OnClosed(e);
    }

    private const string JsonFilter = "Configurações do Dynasland (*.json)|*.json";

    /// <summary>Exportar, importar e restaurar usam diálogos do Windows, que a página não pode abrir.</summary>
    private async Task RunActionAsync(string name)
    {
        switch (name)
        {
            case "export":
                var save = new SaveFileDialog { Filter = JsonFilter, FileName = $"dynasland-configuracoes-{DateTime.Now:yyyyMMdd}.json" };
                if (save.ShowDialog(this) == true)
                {
                    await ViewModel.Storage.ExportAsync(save.FileName);
                }

                break;

            case "import":
                var open = new OpenFileDialog { Filter = JsonFilter };
                if (open.ShowDialog(this) == true
                    && MessageBox.Show(this, "Substituir as configurações atuais pelas do arquivo?", "Importar configurações",
                        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    await ViewModel.Storage.ImportAsync(open.FileName);
                    ViewModel.Refresh();
                }

                break;

            case "restore":
                if (MessageBox.Show(this,
                        "Restaurar todas as configurações do Dynasland para o padrão?\n\nAparência, animações, comportamento, módulos e notificações voltam ao estado original.",
                        "Restaurar configurações", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    await ViewModel.RestoreDefaultsAsync();
                }

                break;
        }
    }

    private async Task InitializeWebAsync()
    {
        var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dynasland", "webview2");
        var environment = await CoreWebView2Environment.CreateAsync(null, userData);
        await _web.EnsureCoreWebView2Async(environment);

        var core = _web.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.SetVirtualHostNameToFolderMapping(Host, Path.Combine(AppContext.BaseDirectory, "Web"), CoreWebView2HostResourceAccessKind.Allow);

        // A página só pode ser a nossa: qualquer outra navegação ou janela nova é bloqueada.
        core.NavigationStarting += (_, e) =>
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || !string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
            }
        };
        core.NewWindowRequested += (_, e) => e.Handled = true;

        _bridge = new SettingsBridge(ViewModel, json => core.PostWebMessageAsString(json), Dispatcher, name => _ = RunActionAsync(name));
        core.WebMessageReceived += (_, e) => _bridge?.Handle(e.TryGetWebMessageAsString());
        core.Navigate($"https://{Host}/settings.html");
    }
}
