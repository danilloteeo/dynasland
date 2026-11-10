using System.Windows;
using System.Windows.Controls;
using Dynasland.App.ViewModels;
using Microsoft.Win32;

namespace Dynasland.App.Views.Settings;

public partial class StoragePage : UserControl
{
    private const string Filter = "Configurações do Dynasland (*.json)|*.json";

    public StoragePage() => InitializeComponent();

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = Filter, FileName = $"dynasland-configuracoes-{DateTime.Now:yyyyMMdd}.json" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true && DataContext is SettingsViewModel viewModel)
        {
            await viewModel.Storage.ExportAsync(dialog.FileName);
        }
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = Filter };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true || DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var answer = MessageBox.Show(Window.GetWindow(this)!, "Substituir as configurações atuais pelas do arquivo?", "Importar configurações",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            await viewModel.Storage.ImportAsync(dialog.FileName);
            viewModel.Refresh();
        }
    }
}
