using Dynasland.Core.Modules;
using Dynasland.Modules.Battery;
using Dynasland.Modules.Brightness;
using Dynasland.Modules.Capture;
using Dynasland.Modules.Clipboard;
using Dynasland.Modules.Clock;
using Dynasland.Modules.Downloads;
using Dynasland.Modules.Gaming;
using Dynasland.Modules.KeyLocks;
using Dynasland.Modules.Life;
using Dynasland.Modules.Media;
using Dynasland.Modules.Notifications;
using Dynasland.Modules.Quotes;
using Dynasland.Modules.Scores;
using Dynasland.Modules.SystemMonitor;
using Dynasland.Modules.Timer;
using Dynasland.Modules.Volume;
using Dynasland.Modules.Water;
using Microsoft.Extensions.DependencyInjection;

namespace Dynasland.Modules;

public static class DependencyInjection
{
    public static IServiceCollection AddDynaslandModules(this IServiceCollection services)
    {
        // A ordem de registro é a ordem exibida nas configurações e nos atalhos da tela inicial.
        services.AddSingleton<IIslandModule, MediaModule>();
        services.AddSingleton<IIslandModule, VolumeModule>();
        services.AddSingleton<IIslandModule, BrightnessModule>();
        services.AddSingleton<IIslandModule, QuotesModule>();
        services.AddSingleton<IIslandModule, ScoresModule>();
        services.AddSingleton<IIslandModule, KeyLockModule>();
        services.AddSingleton<IIslandModule, NotificationsModule>();
        services.AddSingleton<IIslandModule, ClipboardModule>();
        services.AddSingleton<IIslandModule, WeatherModule>();
        services.AddSingleton<IIslandModule, WaterModule>();
        services.AddSingleton<IIslandModule, ClockModule>();

        services.AddSingleton<NotificationPreferences>();
        services.AddSingleton<ClipboardHistory>();
        services.AddSingleton<Dynasland.Core.Interfaces.IClipboardHistory>(sp => sp.GetRequiredService<ClipboardHistory>());

        foreach (var templates in new[]
                 {
                     "Media/MediaTemplates.xaml",
                     "Volume/VolumeTemplates.xaml",
                     "Brightness/BrightnessTemplates.xaml",
                     "Notifications/NotificationTemplates.xaml",
                     "Downloads/DownloadTemplates.xaml",
                     "Battery/BatteryTemplates.xaml",
                     "Timer/TimerTemplates.xaml",
                     "Clipboard/ClipboardTemplates.xaml",
                     "SystemMonitor/SystemMonitorTemplates.xaml",
                     "Capture/CaptureTemplates.xaml",
                     "Gaming/GamingTemplates.xaml",
                     "Life/LifeTemplates.xaml",
                     "Water/WaterTemplates.xaml",
                     "Quotes/QuotesTemplates.xaml",
                     "Scores/ScoresTemplates.xaml",
                     "KeyLocks/KeyLockTemplates.xaml",
                     "Clock/ClockTemplates.xaml",
                 })
        {
            services.AddSingleton(ModuleTemplates.FromComponent(templates));
        }

        return services;
    }
}
