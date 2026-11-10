using System.Windows;
using System.Windows.Threading;
using Dynasland.App.ViewModels.Hub;
using Dynasland.App.Views;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Dynasland.App.Services;

/// <summary>Abre a Central (uma só instância), criada sob demanda e liberada ao fechar.</summary>
public sealed class HubWindowService(IServiceProvider services, Dispatcher dispatcher) : IHubService
{
    private HubWindow? _window;
    private HubViewModel? _viewModel;

    public void Open(HubPage page)
    {
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Open(page));
            return;
        }

        if (_window is null)
        {
            _viewModel = new HubViewModel(services.GetServices<IHubPage>());
            _window = new HubWindow(_viewModel, services.GetRequiredService<ThemeService>());
            _window.Closed += (_, _) =>
            {
                _window = null;
                _viewModel = null;
            };
            _viewModel.Navigate(page);
            _window.Show();
        }
        else
        {
            _viewModel?.Navigate(page);
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
            }
        }

        _window.Activate();
    }
}
