using Dynasland.App.ViewModels;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.Services;

/// <summary>Verifica atualizações uma vez após a inicialização (se ligado) e avisa na ilha.</summary>
public sealed class UpdateNotifier(
    IUpdateChecker checker,
    IIslandSettingsService settings,
    IEventBus bus,
    IShellService shell,
    ILogger<UpdateNotifier> logger)
{
    public const string ActivityId = "app.update";

    // Espera o computador terminar de iniciar antes de usar a rede.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    public async Task CheckAfterStartupAsync()
    {
        if (!checker.IsConfigured || !settings.Current.CheckForUpdates)
        {
            return;
        }

        try
        {
            await Task.Delay(StartupDelay);
            if (!settings.Current.CheckForUpdates)
            {
                return;
            }

            var result = await checker.CheckAsync();
            logger.LogInformation("Verificação de atualizações: {Status} {Version}", result.Status, result.LatestVersion);
            if (result is { Status: UpdateStatus.UpdateAvailable, LatestVersion: { } version, ReleaseUrl: { } url })
            {
                var content = new UpdateAvailableContent(
                    version,
                    download: () =>
                    {
                        shell.OpenUrl(url);
                        bus.RemoveActivity(ActivityId);
                    },
                    dismiss: () => bus.RemoveActivity(ActivityId));
                bus.PostActivity(new IslandActivity(ActivityId, "app", ActivityKind.Transient, content) { Duration = TimeSpan.FromSeconds(10) });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha na verificação automática de atualizações.");
        }
    }
}
