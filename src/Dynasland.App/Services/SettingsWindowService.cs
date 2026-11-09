using System.Windows;
using Dynasland.App.ViewModels;
using Dynasland.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Dynasland.App.Services;

/// <summary>Abre a janela de configurações (uma só instância; reabrir apenas a traz para frente).</summary>
public sealed class SettingsWindowService(IServiceProvider services)
{
    private SettingsWindow? _window;

    public void Show()
    {
        if (_window is not null)
        {
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
            }

            _window.Activate();
            return;
        }

        _window = new SettingsWindow(services.GetRequiredService<SettingsViewModel>(), services.GetRequiredService<ThemeService>());
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
    }
}
