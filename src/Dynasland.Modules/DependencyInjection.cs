using Dynasland.Core.Modules;
using Dynasland.Modules.Battery;
using Dynasland.Modules.Brightness;
using Dynasland.Modules.Capture;
using Dynasland.Modules.Clipboard;
using Dynasland.Modules.Clock;
using Dynasland.Modules.Downloads;
using Dynasland.Modules.Life;
using Dynasland.Modules.Media;
using Dynasland.Modules.Notifications;
using Dynasland.Modules.SystemMonitor;
using Dynasland.Modules.Timer;
using Dynasland.Modules.Volume;
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
        services.AddSingleton<IIslandModule, NotificationsModule>();
        services.AddSingleton<IIslandModule, DownloadsModule>();
        services.AddSingleton<IIslandModule, BatteryModule>();
        services.AddSingleton<IIslandModule, TimerModule>();
        services.AddSingleton<IIslandModule, ClipboardModule>();
        services.AddSingleton<IIslandModule, SystemMonitorModule>();
        services.AddSingleton<IIslandModule, CaptureModule>();
        services.AddSingleton<IIslandModule, WeatherModule>();
        services.AddSingleton<IIslandModule, CalendarModule>();
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
                     "Life/LifeTemplates.xaml",
                     "Clock/ClockTemplates.xaml",
                 })
        {
            services.AddSingleton(ModuleTemplates.FromComponent(templates));
        }

        return services;
    }
}
