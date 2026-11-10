using System.Windows;
using System.Windows.Controls;
using Dynasland.App.ViewModels;

namespace Dynasland.App.Views.Settings;

public partial class PrivacyPage : UserControl
{
    public PrivacyPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.Privacy.Confirm = message => MessageBox.Show(
                    Window.GetWindow(this)!, message, "Privacidade", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            }
        };
    }
}
