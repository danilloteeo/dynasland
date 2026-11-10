using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Dynasland.App.Services;
using Dynasland.App.ViewModels;
using Dynasland.App.Views.Settings;

namespace Dynasland.App.Views;

/// <summary>Janela de configurações: navegação lateral e uma página por categoria, criada ao ser aberta.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel, ThemeService theme)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        TitleBarTheme.Attach(this, theme);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ShowSection(viewModel.Section);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.Section))
        {
            ShowSection(_viewModel.Section);
        }
    }

    /// <summary>Cada seção começa do topo, com a página criada sob demanda (herda o DataContext).</summary>
    private void ShowSection(SettingsSection section)
    {
        PageHost.Content = CreatePage(section);
        ContentScroll.ScrollToTop();
    }

    private static UserControl CreatePage(SettingsSection section) => section switch
    {
        SettingsSection.Appearance => new AppearancePage(),
        SettingsSection.Animations => new AnimationsPage(),
        SettingsSection.Behavior => new BehaviorPage(),
        SettingsSection.Monitors => new MonitorsPage(),
        SettingsSection.Modules => new ModulesPage(),
        SettingsSection.Notifications => new NotificationsPage(),
        SettingsSection.Privacy => new PrivacyPage(),
        SettingsSection.Shortcuts => new ShortcutsPage(),
        SettingsSection.Storage => new StoragePage(),
        SettingsSection.About => new AboutPage(),
        _ => new GeneralPage(),
    };
}
