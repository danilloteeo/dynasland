using Dynasland.Core.Modules;
using Dynasland.Modules.Clock;
using Microsoft.Extensions.DependencyInjection;

namespace Dynasland.Modules;

public static class DependencyInjection
{
    public static IServiceCollection AddDynaslandModules(this IServiceCollection services)
    {
        services.AddSingleton<IIslandModule, ClockModule>();
        services.AddSingleton(ModuleTemplates.FromComponent("Clock/ClockTemplates.xaml"));

        return services;
    }
}
