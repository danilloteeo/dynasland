using System.Windows;
using System.Windows.Controls;
using DynaIsland.App.ViewModels.Hub;

namespace DynaIsland.App.Views.Hub;

public partial class NotificationsPage : UserControl
{
    public NotificationsPage() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is NotificationsPageViewModel viewModel && Window.GetWindow(this) is HubWindow window)
        {
            viewModel.Confirm = window.Confirm;
        }
    }
}
