using DynaIsland.Core.Modules;
using DynaIsland.Modules.Battery;
using DynaIsland.Modules.Brightness;
using DynaIsland.Modules.Capture;
using DynaIsland.Modules.Clipboard;
using DynaIsland.Modules.Clock;
using DynaIsland.Modules.Downloads;
using DynaIsland.Modules.Gaming;
using DynaIsland.Modules.KeyLocks;
using DynaIsland.Modules.Life;
using DynaIsland.Modules.Media;
using DynaIsland.Modules.Notifications;
using DynaIsland.Modules.Quotes;
using DynaIsland.Modules.Scores;
using DynaIsland.Modules.SystemMonitor;
using DynaIsland.Modules.Timer;
using DynaIsland.Modules.Volume;
using DynaIsland.Modules.Water;
using Microsoft.Extensions.DependencyInjection;

namespace DynaIsland.Modules;

public static class DependencyInjection
{
    public static IServiceCollection AddDynaIslandModules(this IServiceCollection services)
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
        services.AddSingleton<DynaIsland.Core.Interfaces.IClipboardHistory>(sp => sp.GetRequiredService<ClipboardHistory>());

        foreach (var templates in new[]
                 {
                     "Media/MediaTemplates.xaml",
                     "Volume/VolumeTemplates.xaml",
                     "Brightness/BrightnessTemplates.xaml",
                     "Notifications/NotificationTemplates.xaml",
                     "Downloads/DownloadTemplates.xaml",
                     "Battery/BatteryTemplates.xaml",
                     "Bluetooth/BluetoothTemplates.xaml",
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
