using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Notifications;

/// <summary>Exibe notificações do Windows na ilha, com ações de abrir, dispensar e silenciar o aplicativo.</summary>
public sealed class NotificationsModule(
    INotificationService notifications,
    NotificationPreferences preferences,
    IModuleOptions options,
    IEventBus bus,
    ILogger<NotificationsModule> logger) : IIslandModule
{
    public const string ModuleId = "notifications";
    private const int LogoDecodeWidth = 72;

    public static readonly ModuleOption ShowMessageText = ModuleOption.Toggle(
        "showMessageText", "Mostrar o texto das mensagens", "Desligado, a ilha mostra apenas o aplicativo e o título (mais privacidade).");

    public string Id => ModuleId;

    public string DisplayName => "Notificações";

    public string Description => "Notificações do Windows, com opção de silenciar aplicativos.";

    public IReadOnlyList<ModuleOption> Options => [ShowMessageText];

    public static string ActivityIdFor(uint notificationId) => $"notification.{notificationId}";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await preferences.LoadAsync(cancellationToken);

        switch (await notifications.StartAsync(cancellationToken))
        {
            case NotificationAccess.Denied:
                throw new ModuleUnavailableException(
                    "Acesso negado. Permita em Configurações > Privacidade > Notificações.");
            case NotificationAccess.Unavailable:
                throw new ModuleUnavailableException("Leitura de notificações indisponível nesta versão do Windows.");
        }

        notifications.Received += OnReceived;
        notifications.Removed += OnRemoved;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        notifications.Received -= OnReceived;
        notifications.Removed -= OnRemoved;
        await notifications.StopAsync();
    }

    private void OnReceived(object? sender, NotificationInfo info)
    {
        _ = RememberAsync(info);
        if (!preferences.ShouldShow(info.AppId))
        {
            return;
        }

        var content = new NotificationContent(info, ImageDecoder.Decode(info.AppLogo, LogoDecodeWidth), options.IsOn(ModuleId, ShowMessageText), Open, Dismiss, MuteApp);
        bus.PostActivity(new IslandActivity(ActivityIdFor(info.Id), ModuleId, ActivityKind.Transient, content)
        {
            Duration = TimeSpan.FromSeconds(preferences.DurationSeconds),
            Priority = preferences.CanInterrupt ? ActivityPriority.High : ActivityPriority.Normal,
        });
    }

    /// <summary>Guarda o aplicativo para que ele apareça na lista das configurações.</summary>
    private async Task RememberAsync(NotificationInfo info)
    {
        try
        {
            await preferences.RememberAppAsync(info.AppId, info.AppName);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Falha ao registrar {App}.", info.AppName);
        }
    }

    private void OnRemoved(object? sender, uint notificationId) => bus.RemoveActivity(ActivityIdFor(notificationId));

    private void Open(NotificationInfo info)
    {
        notifications.TryOpenApp(info.AppId);
        bus.RemoveActivity(ActivityIdFor(info.Id));
    }

    private void Dismiss(NotificationInfo info)
    {
        notifications.Dismiss(info.Id);
        bus.RemoveActivity(ActivityIdFor(info.Id));
    }

    private async void MuteApp(NotificationInfo info)
    {
        bus.RemoveActivity(ActivityIdFor(info.Id));
        try
        {
            await preferences.SetBlockedAsync(info.AppId, blocked: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao silenciar {App}.", info.AppName);
        }
    }
}
