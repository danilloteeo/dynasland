using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dynasland.App.ViewModels.Hub;

namespace Dynasland.App.Views.Hub;

public partial class ClipboardPage : UserControl
{
    public ClipboardPage() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClipboardPageViewModel viewModel && Window.GetWindow(this) is HubWindow window)
        {
            viewModel.Confirm = window.Confirm;
        }

        Search.Focus();
    }

    /// <summary>Duplo clique copia o item.</summary>
    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ClipboardPageViewModel viewModel && viewModel.Selected is { } item)
        {
            viewModel.CopyCommand.Execute(item);
        }
    }
}
