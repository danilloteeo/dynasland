using System.Windows;
using System.Windows.Threading;
using Dynasland.App.Services;
using Dynasland.App.ViewModels;
using Dynasland.App.Views;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;
using Dynasland.Infrastructure;
using Dynasland.Infrastructure.Database;
using Dynasland.Infrastructure.Logging;
using Dynasland.Infrastructure.Services;
using Dynasland.Infrastructure.Windows;
using Dynasland.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dynasland.App;

public partial class App : Application
{
    private const string ApplicationId = "Dynasland";

    private SingleInstanceManager? _singleInstance;
    private IHost? _host;
    private ILogger<App>? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceManager(ApplicationId);
        if (!_singleInstance.TryAcquire())
        {
            // Já existe uma instância: pede que ela mostre a ilha e encerra esta.
            _singleInstance.SignalFirstInstance();
            Shutdown();
            return;
        }

        RegisterGlobalExceptionHandlers();

        try
        {
            _host = BuildHost();
            _logger = _host.Services.GetRequiredService<ILogger<App>>();

            var settings = _host.Services.GetRequiredService<IIslandSettingsService>();
            string? corruptedDatabaseBackup = null;
            await Task.Run(async () =>
            {
                corruptedDatabaseBackup = await _host.Services.GetRequiredService<DatabaseRecovery>().MigrateOrRecoverAsync();
                await settings.LoadAsync();
                await _host.Services.GetRequiredService<IModuleOptions>().LoadAsync();
                await _host.Services.GetRequiredService<IGlobalShortcutRegistry>().LoadAsync();

                // "Iniciar minimizado": abre com a ilha oculta (o usuário mostra pela bandeja).
                if (settings.Current.StartHidden && settings.Current.IsVisible)
                {
                    await settings.UpdateAsync(s => s with { IsVisible = false });
                }
            });
            MergeModuleTemplates(_host.Services);
            _host.Services.GetRequiredService<ThemeService>().Initialize(this);

            // A ilha (e o controlador que escuta o barramento) existe antes dos módulos iniciarem,
            // para que nenhuma atividade publicada na partida seja perdida.
            _host.Services.GetRequiredService<SessionStateService>().Start();
            var island = _host.Services.GetRequiredService<IslandWindow>();
            island.Start();

            await _host.StartAsync();

            // Último atalho da tela inicial da ilha, depois dos registrados pelos módulos.
            var settingsWindow = _host.Services.GetRequiredService<SettingsWindowService>();
            _host.Services.GetRequiredService<IShortcutRegistry>().Register(
                new IslandShortcut("app.settings", "app", "", "Configurações", settingsWindow.Show));

            _host.Services.GetRequiredService<IslandActions>().RegisterShortcuts();
            _host.Services.GetRequiredService<HotkeyService>().Start();

            var tray = _host.Services.GetRequiredService<TrayIconService>();
            tray.ExitRequested += (_, _) => Shutdown();
            tray.Initialize();

            if (e.Args.Contains("--configuracoes", StringComparer.OrdinalIgnoreCase))
            {
                settingsWindow.Show();
            }

            _singleInstance.ActivationRequested += (_, _) =>
                Dispatcher.BeginInvoke(async () => await settings.UpdateAsync(s => s with { IsVisible = true }));

            _ = _host.Services.GetRequiredService<UpdateNotifier>().CheckAfterStartupAsync();

            if (corruptedDatabaseBackup is not null)
            {
                _ = Dispatcher.BeginInvoke(() => MessageBox.Show(
                    "O banco de dados de configurações estava danificado e foi recriado com os valores padrão.\n\n"
                        + $"Uma cópia do arquivo antigo foi guardada em:\n{corruptedDatabaseBackup}",
                    "Dynasland",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning));
            }

            _logger.LogInformation("Dynasland iniciado.");
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Falha ao iniciar o Dynasland.");
            MessageBox.Show(
                $"Não foi possível iniciar o Dynasland.\n\n{ex.Message}",
                "Dynasland",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services.GetService<TrayIconService>()?.Dispose();
            _host.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void MergeModuleTemplates(IServiceProvider services)
    {
        foreach (var templates in services.GetServices<ModuleTemplates>())
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = templates.Source });
        }
    }

    private IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = ApplicationId,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Logging.AddProvider(new FileLoggerProvider(FileLoggerProvider.DefaultPath));

        builder.Services.AddDynaslandInfrastructure(new DatabaseOptions(DatabaseOptions.DefaultPath));
        builder.Services.AddDynaslandModules();
        builder.Services.AddSingleton<IUiDispatcher>(new WpfUiDispatcher(Dispatcher));
        builder.Services.AddHostedService<ModuleHostedService>();

        builder.Services.AddSingleton<IslandController>();
        builder.Services.AddSingleton<IslandPlacementService>();
        builder.Services.AddSingleton<IslandViewModel>();
        builder.Services.AddSingleton<IslandWindow>();
        builder.Services.AddSingleton<TrayIconService>();
        builder.Services.AddSingleton<IAlertSound, AlertSoundPlayer>();
        builder.Services.AddSingleton<SettingsWindowService>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<FullscreenDetector>();
        builder.Services.AddSingleton<UpdateNotifier>();
        builder.Services.AddSingleton<SessionStateService>();
        builder.Services.AddSingleton<HotkeyService>();
        builder.Services.AddSingleton<IslandActions>();
        builder.Services.AddSingleton<ISessionStateService>(sp => sp.GetRequiredService<SessionStateService>());

        return builder.Build();
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger?.LogCritical(args.ExceptionObject as Exception, "Exceção não tratada.");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Exceção não observada em tarefa.");
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Mantém o aplicativo vivo: um erro em um módulo não deve derrubar a ilha.
        _logger?.LogError(e.Exception, "Exceção não tratada na thread de interface.");
        e.Handled = true;
    }
}
