using System.Windows;
using System.Windows.Controls;
using Dynasland.App.ViewModels.Hub;

namespace Dynasland.App.Views.Hub;

public partial class CalendarPage : UserControl
{
    public CalendarPage() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CalendarPageViewModel viewModel && Window.GetWindow(this) is HubWindow window)
        {
            viewModel.Confirm = window.Confirm;
        }
    }
}
