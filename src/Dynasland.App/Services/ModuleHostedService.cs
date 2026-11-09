using Dynasland.Core.Modules;
using Microsoft.Extensions.Hosting;

namespace Dynasland.App.Services;

/// <summary>Liga o ciclo de vida dos módulos ao do Generic Host.</summary>
public sealed class ModuleHostedService(ModuleManager moduleManager) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => moduleManager.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => moduleManager.StopAsync(cancellationToken);
}
