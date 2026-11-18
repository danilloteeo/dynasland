using DynaIsland.Core.Events;
using DynaIsland.Core.Interfaces;
using DynaIsland.Core.Modules;
using DynaIsland.Core.Services;
using DynaIsland.Core.Shortcuts;
using DynaIsland.Infrastructure.Database;
using DynaIsland.Infrastructure.Database.Migrations;
using DynaIsland.Infrastructure.Lyrics;
using DynaIsland.Infrastructure.Windows.Bluetooth;
using DynaIsland.Infrastructure.Repositories;
using DynaIsland.Infrastructure.Spotify;
using DynaIsland.Infrastructure.Updates;
using DynaIsland.Infrastructure.Weather;
using DynaIsland.Infrastructure.Windows;
using DynaIsland.Infrastructure.Windows.Audio;
using DynaIsland.Infrastructure.Windows.Brightness;
using DynaIsland.Infrastructure.Windows.Capture;
using DynaIsland.Infrastructure.Windows.Gaming;
using DynaIsland.Infrastructure.Windows.Clipboard;
using DynaIsland.Infrastructure.Windows.Downloads;
using DynaIsland.Infrastructure.Windows.Metrics;
using DynaIsland.Infrastructure.Windows.Media;
using DynaIsland.Infrastructure.Windows.Notifications;
using DynaIsland.Infrastructure.Windows.Power;
using DynaIsland.Infrastructure.Windows.Privacy;
using Microsoft.Extensions.DependencyInjection;

namespace DynaIsland.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDynaIslandInfrastructure(this IServiceCollection services, DatabaseOptions databaseOptions)
    {
        services.AddSingleton(databaseOptions);
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<IMigration, M001_InitialSchema>();
        services.AddSingleton<IMigration, M002_Histories>();
        services.AddSingleton<IMigration, M003_WaterLog>();
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<DatabaseRecovery>();

        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<IModuleStateStore, SqliteModuleStateStore>();
        services.AddSingleton<IClipboardHistoryStore, SqliteClipboardHistoryStore>();
        services.AddSingleton<INotificationHistoryStore, SqliteNotificationHistoryStore>();
        services.AddSingleton<IFocusSessionStore, SqliteFocusSessionStore>();
        services.AddSingleton<ICalendarStore, SqliteCalendarStore>();
        services.AddSingleton<IWaterStore, SqliteWaterStore>();
        services.AddSingleton<ICaptureHistoryStore, SqliteCaptureHistoryStore>();
        services.AddSingleton<IWeatherCache, SqliteWeatherCache>();
        services.AddSingleton<IStorageMaintenance, StorageMaintenance>();
        services.AddSingleton<IIslandSettingsService, IslandSettingsService>();
        services.AddSingleton<IMonitorService, MonitorService>();

        services.AddSingleton<IMediaSessionService, GsmtcMediaSessionService>();
        services.AddSingleton<IAudioVolumeService, CoreAudioVolumeService>();
        services.AddSingleton<IBrightnessService, WmiBrightnessService>();
        services.AddSingleton<IExternalBrightnessService, DdcBrightnessService>();
        services.AddSingleton<IScreenshotService, ScreenshotService>();
        services.AddSingleton<IScreenRecorder, ScreenRecorder>();
        services.AddSingleton<IGameDetector, GameDetector>();
        services.AddSingleton<ISpotifyService>(sp => new SpotifyService(
            new HttpClient { Timeout = TimeSpan.FromSeconds(15) },
            sp.GetRequiredService<IIslandSettingsService>(),
            sp.GetRequiredService<IShellService>(),
            sp.GetRequiredService<DatabaseOptions>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SpotifyService>>()));
        services.AddSingleton<IFpsProvider, EtwFpsProvider>();
        services.AddSingleton<IAudioDeviceService, CoreAudioDeviceService>();
        services.AddSingleton<IAppVolumeService, CoreAudioAppVolumeService>();
        services.AddSingleton<IBluetoothService, WinRtBluetoothService>();
        services.AddSingleton<IPrivacyIndicatorService, RegistryPrivacyIndicatorService>();
        services.AddSingleton<ILyricsService>(sp => new LrcLibLyricsService(
            new HttpClient { Timeout = TimeSpan.FromSeconds(12), DefaultRequestHeaders = { { "User-Agent", "DynaIsland (https://github.com/danilloteeo/dynasland)" } } },
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LrcLibLyricsService>>()));
        services.AddSingleton<IPowerService, WindowsPowerService>();
        services.AddSingleton<INotificationService, WindowsNotificationService>();
        services.AddSingleton<ISystemMetricsService, WindowsSystemMetricsService>();
        services.AddSingleton<IDownloadProvider, BrowserDownloadFolderProvider>();
        services.AddSingleton<IClipboardService, WindowsClipboardService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IShortcutRegistry, ShortcutRegistry>();
        services.AddSingleton<IDockBadges, DockBadgeRegistry>();
        services.AddSingleton<IGlanceRegistry, GlanceRegistry>();
        services.AddSingleton<IIslandTint, IslandTint>();
        services.AddSingleton<IGlobalShortcutRegistry, GlobalShortcutRegistry>();
        services.AddSingleton<IModuleOptions, ModuleOptionsService>();
        services.AddSingleton<IStartupRegistration, StartupRegistration>();
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();
        services.AddSingleton<IUpdateInstaller>(sp => new UpdateInstaller(
            new HttpClient { Timeout = TimeSpan.FromMinutes(30), DefaultRequestHeaders = { { "User-Agent", "DynaIsland" } } },
            UpdateInstallerOptions.Default(databaseOptions.DatabasePath),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UpdateInstaller>>()));
        services.AddSingleton<IWeatherProvider>(sp => new OpenMeteoWeatherProvider(
            new HttpClient { Timeout = TimeSpan.FromSeconds(15), DefaultRequestHeaders = { { "User-Agent", "DynaIsland" } } },
            sp.GetRequiredService<TimeProvider>()));

        services.AddSingleton<IQuotesProvider>(_ => new Market.AwesomeApiQuotesProvider(
            new HttpClient { Timeout = TimeSpan.FromSeconds(15), DefaultRequestHeaders = { { "User-Agent", "DynaIsland" } } }));
        services.AddSingleton<IScoresProvider>(_ => new Market.EspnScoresProvider(
            new HttpClient { Timeout = TimeSpan.FromSeconds(15), DefaultRequestHeaders = { { "User-Agent", "DynaIsland" } } }));
        services.AddSingleton<IKeyLockService, Windows.KeyLockService>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<ModuleManager>();

        return services;
    }
}
