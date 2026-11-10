using System.Windows;
using System.Windows.Controls;
using Dynasland.App.ViewModels;

namespace Dynasland.App.Views.Settings;

public partial class AboutPage : UserControl
{
    public AboutPage() => InitializeComponent();

    private async void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            Window.GetWindow(this)!,
            "Restaurar todas as configurações do Dynasland para o padrão?\n\nAparência, animações, comportamento, módulos, notificações e atalhos voltam ao estado original.",
            "Restaurar configurações",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (answer == MessageBoxResult.Yes && DataContext is SettingsViewModel viewModel)
        {
            await viewModel.RestoreDefaultsAsync();
        }
    }
}
