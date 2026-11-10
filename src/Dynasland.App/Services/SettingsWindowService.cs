using System.Windows;
using Dynasland.App.ViewModels;
using Dynasland.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Dynasland.App.Services;

/// <summary>Abre a janela de configurações (uma só instância; reabrir apenas a traz para frente).</summary>
public sealed class SettingsWindowService(IServiceProvider services)
{
    private SettingsWindow? _window;

    public void Show() => Show(null);

    /// <param name="section">Página a exibir; <c>null</c> mantém a atual.</param>
    public void Show(SettingsSection? section)
    {
        if (_window is not null)
        {
            if (section is { } target && _window.DataContext is SettingsViewModel current)
            {
                current.Section = target;
            }

            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
            }

            _window.Activate();
            return;
        }

        var viewModel = services.GetRequiredService<SettingsViewModel>();
        if (section is { } initial)
        {
            viewModel.Section = initial;
        }

        _window = new SettingsWindow(viewModel, services.GetRequiredService<ThemeService>());
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
    }
}
