using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Core.Services;
using Dynasland.Core.Shortcuts;
using Dynasland.Infrastructure.Database;
using Dynasland.Infrastructure.Database.Migrations;
using Dynasland.Infrastructure.Lyrics;
using Dynasland.Infrastructure.Windows.Bluetooth;
using Dynasland.Infrastructure.Repositories;
using Dynasland.Infrastructure.Spotify;
using Dynasland.Infrastructure.Updates;
using Dynasland.Infrastructure.Weather;
using Dynasland.Infrastructure.Windows;
using Dynasland.Infrastructure.Windows.Audio;
using Dynasland.Infrastructure.Windows.Brightness;
using Dynasland.Infrastructure.Windows.Capture;
using Dynasland.Infrastructure.Windows.Gaming;
using Dynasland.Infrastructure.Windows.Clipboard;
using Dynasland.Infrastructure.Windows.Downloads;
using Dynasland.Infrastructure.Windows.Metrics;
using Dynasland.Infrastructure.Windows.Media;
using Dynasland.Infrastructure.Windows.Notifications;
using Dynasland.Infrastructure.Windows.Power;
using Dynasland.Infrastructure.Windows.Privacy;
using Microsoft.Extensions.DependencyInjection;

namespace Dynasland.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDynaslandInfrastructure(this IServiceCollection services, DatabaseOptions databaseOptions)
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
            new HttpClient { Timeout = TimeSpan.FromSeconds(12), DefaultRequestHeaders = { { "User-Agent", "Dynasland (https://github.com/danilloteeo/dynasland)" } } },
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LrcLibLyricsService>>()));
        services.AddSingleton<IPowerService, WindowsPowerService>();
        services.AddSingleton<INotificationService, WindowsNotificationService>();
        services.AddSingleton<ISystemMetricsService, WindowsSystemMetricsService>();
        services.AddSingleton<IDownloadProvider, BrowserDownloadFolderProvider>();
        services.AddSingleton<IClipboardService, WindowsClipboardService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IShortcutRegistry, ShortcutRegistry>();
        services.AddSingleton<IGlanceRegistry, GlanceRegistry>();
        services.AddSingleton<IIslandTint, IslandTint>();
        services.AddSingleton<IGlobalShortcutRegistry, GlobalShortcutRegistry>();
        services.AddSingleton<IModuleOptions, ModuleOptionsService>();
        services.AddSingleton<IStartupRegistration, StartupRegistration>();
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();
        services.AddSingleton<IUpdateInstaller>(sp => new UpdateInstaller(
            new HttpClient { Timeout = TimeSpan.FromMinutes(30), DefaultRequestHeaders = { { "User-Agent", "Dynasland" } } },
            UpdateInstallerOptions.Default(databaseOptions.DatabasePath),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UpdateInstaller>>()));
        services.AddSingleton<IWeatherProvider>(sp => new OpenMeteoWeatherProvider(
            new HttpClient { Timeout = TimeSpan.FromSeconds(15), DefaultRequestHeaders = { { "User-Agent", "Dynasland" } } },
            sp.GetRequiredService<TimeProvider>()));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<ModuleManager>();

        return services;
    }
}
