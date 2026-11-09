using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Core.Services;
using Dynasland.Infrastructure.Database;
using Dynasland.Infrastructure.Database.Migrations;
using Dynasland.Infrastructure.Repositories;
using Dynasland.Infrastructure.Updates;
using Dynasland.Infrastructure.Windows;
using Dynasland.Infrastructure.Windows.Audio;
using Dynasland.Infrastructure.Windows.Brightness;
using Dynasland.Infrastructure.Windows.Clipboard;
using Dynasland.Infrastructure.Windows.Downloads;
using Dynasland.Infrastructure.Windows.Metrics;
using Dynasland.Infrastructure.Windows.Media;
using Dynasland.Infrastructure.Windows.Notifications;
using Dynasland.Infrastructure.Windows.Power;
using Microsoft.Extensions.DependencyInjection;

namespace Dynasland.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDynaslandInfrastructure(this IServiceCollection services, DatabaseOptions databaseOptions)
    {
        services.AddSingleton(databaseOptions);
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<IMigration, M001_InitialSchema>();
        services.AddSingleton<MigrationRunner>();

        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<IModuleStateStore, SqliteModuleStateStore>();
        services.AddSingleton<IIslandSettingsService, IslandSettingsService>();
        services.AddSingleton<IMonitorService, MonitorService>();

        services.AddSingleton<IMediaSessionService, GsmtcMediaSessionService>();
        services.AddSingleton<IAudioVolumeService, CoreAudioVolumeService>();
        services.AddSingleton<IBrightnessService, WmiBrightnessService>();
        services.AddSingleton<IPowerService, WindowsPowerService>();
        services.AddSingleton<INotificationService, WindowsNotificationService>();
        services.AddSingleton<ISystemMetricsService, WindowsSystemMetricsService>();
        services.AddSingleton<IDownloadProvider, BrowserDownloadFolderProvider>();
        services.AddSingleton<IClipboardService, WindowsClipboardService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IShortcutRegistry, ShortcutRegistry>();
        services.AddSingleton<IModuleOptions, ModuleOptionsService>();
        services.AddSingleton<IStartupRegistration, StartupRegistration>();
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<ModuleManager>();

        return services;
    }
}
