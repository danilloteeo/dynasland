using Dynasland.Core.Modules;
using Dynasland.Modules.Battery;
using Dynasland.Modules.Brightness;
using Dynasland.Modules.Clock;
using Dynasland.Modules.Media;
using Dynasland.Modules.Notifications;
using Dynasland.Modules.Volume;
using Microsoft.Extensions.DependencyInjection;

namespace Dynasland.Modules;

public static class DependencyInjection
{
    public static IServiceCollection AddDynaslandModules(this IServiceCollection services)
    {
        // A ordem de registro é a ordem exibida no menu de módulos.
        services.AddSingleton<IIslandModule, MediaModule>();
        services.AddSingleton<IIslandModule, VolumeModule>();
        services.AddSingleton<IIslandModule, BrightnessModule>();
        services.AddSingleton<IIslandModule, NotificationsModule>();
        services.AddSingleton<IIslandModule, BatteryModule>();
        services.AddSingleton<IIslandModule, ClockModule>();

        services.AddSingleton<NotificationPreferences>();

        services.AddSingleton(ModuleTemplates.FromComponent("Media/MediaTemplates.xaml"));
        services.AddSingleton(ModuleTemplates.FromComponent("Volume/VolumeTemplates.xaml"));
        services.AddSingleton(ModuleTemplates.FromComponent("Brightness/BrightnessTemplates.xaml"));
        services.AddSingleton(ModuleTemplates.FromComponent("Notifications/NotificationTemplates.xaml"));
        services.AddSingleton(ModuleTemplates.FromComponent("Battery/BatteryTemplates.xaml"));
        services.AddSingleton(ModuleTemplates.FromComponent("Clock/ClockTemplates.xaml"));

        return services;
    }
}
