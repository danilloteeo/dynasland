using System.Windows;
using DynaIsland.App.Services;
using DynaIsland.App.ViewModels.Hub;

namespace DynaIsland.App.Views;

public partial class HubWindow : Window
{
    private readonly HubViewModel _viewModel;

    public HubWindow(HubViewModel viewModel, ThemeService theme)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        TitleBarTheme.Attach(this, theme);
    }

    /// <summary>Confirmação padrão das páginas (limpar históricos, excluir compromissos).</summary>
    public bool Confirm(string message) =>
        MessageBox.Show(this, message, "Central do DynaIsland", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
