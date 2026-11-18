using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DynaIsland.App.ViewModels.Hub;

namespace DynaIsland.App.Views.Hub;

public partial class CapturesPage : UserControl
{
    public CapturesPage() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CapturesPageViewModel viewModel && Window.GetWindow(this) is HubWindow window)
        {
            viewModel.Confirm = window.Confirm;
        }
    }

    /// <summary>Duplo clique abre a captura.</summary>
    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is CapturesPageViewModel viewModel && List.SelectedItem is CaptureItemViewModel item)
        {
            viewModel.OpenCommand.Execute(item);
        }
    }
}
