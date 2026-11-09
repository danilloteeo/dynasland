using Dynasland.Core.Interfaces;
using Dynasland.Core.Services;
using Dynasland.Infrastructure.Database;
using Dynasland.Infrastructure.Database.Migrations;
using Dynasland.Infrastructure.Repositories;
using Dynasland.Infrastructure.Windows;
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
        services.AddSingleton<IIslandSettingsService, IslandSettingsService>();
        services.AddSingleton<IMonitorService, MonitorService>();

        return services;
    }
}
