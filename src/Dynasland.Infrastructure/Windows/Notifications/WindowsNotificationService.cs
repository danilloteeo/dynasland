using System.Collections.Concurrent;
using System.Diagnostics;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Dynasland.Infrastructure.Windows.Notifications;

/// <summary>
/// Lê as notificações da Central de Ações via UserNotificationListener.
/// </summary>
/// <remarks>
/// O evento <c>NotificationChanged</c> só funciona para aplicativos com identidade de pacote (MSIX).
/// Sem ela, o serviço compara a lista de notificações a cada <see cref="PollInterval"/> — uma chamada
/// local barata que só gera trabalho quando o conjunto de IDs muda. Notificações já existentes na
/// inicialização não são reexibidas.
/// </remarks>
public sealed class WindowsNotificationService(TimeProvider time, ILogger<WindowsNotificationService> logger) : INotificationService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);
    private static readonly global::Windows.Foundation.Size LogoSize = new(144, 144);

    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private readonly ConcurrentDictionary<string, byte[]?> _logoCache = new(StringComparer.OrdinalIgnoreCase);

    private UserNotificationListener? _listener;
    private HashSet<uint> _known = [];
    private ITimer? _pollTimer;

    public event EventHandler<NotificationInfo>? Received;

    public event EventHandler<uint>? Removed;

    public async Task<NotificationAccess> StartAsync(CancellationToken cancellationToken = default)
    {
        UserNotificationListener listener;
        UserNotificationListenerAccessStatus access;
        try
        {
            listener = UserNotificationListener.Current;
            access = await listener.RequestAccessAsync().AsTask(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Leitura de notificações indisponível.");
            return NotificationAccess.Unavailable;
        }

        if (access != UserNotificationListenerAccessStatus.Allowed)
        {
            return access == UserNotificationListenerAccessStatus.Denied ? NotificationAccess.Denied : NotificationAccess.Unavailable;
        }

        _listener = listener;
        _known = (await listener.GetNotificationsAsync(NotificationKinds.Toast)).Select(n => n.Id).ToHashSet();

        try
        {
            listener.NotificationChanged += OnNotificationChanged;
            logger.LogInformation("Notificações: usando eventos do Windows.");
        }
        catch (Exception)
        {
            _pollTimer = time.CreateTimer(_ => _ = SyncAsync(), null, PollInterval, PollInterval);
            logger.LogInformation("Notificações: sem identidade de pacote, usando verificação a cada {Interval}.", PollInterval);
        }

        return NotificationAccess.Allowed;
    }

    public Task StopAsync()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;

        if (_listener is not null)
        {
            try
            {
                _listener.NotificationChanged -= OnNotificationChanged;
            }
            catch (Exception)
            {
                // Nunca foi inscrito (modo de verificação periódica).
            }

            _listener = null;
        }

        return Task.CompletedTask;
    }

    public void Dismiss(uint notificationId)
    {
        if (_listener is not { } listener)
        {
            return;
        }

        // O listener foi obtido numa thread MTA; chamado da thread de UI (STA) a remoção não tem efeito.
        _ = Task.Run(() =>
        {
            try
            {
                listener.RemoveNotification(notificationId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Não foi possível dispensar a notificação {Id}.", notificationId);
            }
        });
    }

    public bool TryOpenApp(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return false;
        }

        try
        {
            // shell:AppsFolder abre tanto aplicativos empacotados quanto Win32 registrados com AppUserModelId.
            Process.Start(new ProcessStartInfo("explorer.exe", $@"shell:AppsFolder\{appId}") { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível abrir {App}.", appId);
            return false;
        }
    }

    private async void OnNotificationChanged(UserNotificationListener sender, global::Windows.UI.Notifications.UserNotificationChangedEventArgs args)
    {
        try
        {
            if (args.ChangeKind == UserNotificationChangedKind.Removed)
            {
                Removed?.Invoke(this, args.UserNotificationId);
                return;
            }

            var notification = sender.GetNotification(args.UserNotificationId);
            if (notification is not null && await ConvertAsync(notification) is { } info)
            {
                Received?.Invoke(this, info);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Falha ao processar notificação.");
        }
    }

    private async Task SyncAsync()
    {
        // Se a verificação anterior ainda está em andamento, esta é descartada.
        if (_listener is not { } listener || !await _syncGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);
            var current = new HashSet<uint>();
            foreach (var notification in notifications)
            {
                current.Add(notification.Id);
                if (!_known.Contains(notification.Id) && await ConvertAsync(notification) is { } info)
                {
                    Received?.Invoke(this, info);
                }
            }

            foreach (var removed in _known.Where(id => !current.Contains(id)))
            {
                Removed?.Invoke(this, removed);
            }

            _known = current;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Falha ao verificar notificações.");
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task<NotificationInfo?> ConvertAsync(UserNotification notification)
    {
        var binding = notification.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
        var texts = binding?.GetTextElements()
            .Select(t => t.Text?.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Cast<string>()
            .ToList() ?? [];

        if (texts.Count == 0)
        {
            return null;
        }

        var title = texts.Count > 1 ? texts[0] : null;
        var body = string.Join("\n", texts.Count > 1 ? texts.Skip(1) : texts);

        var appInfo = notification.AppInfo;
        var appId = appInfo?.AppUserModelId ?? string.Empty;
        var appName = appInfo?.DisplayInfo?.DisplayName;

        return new NotificationInfo(
            notification.Id,
            appId,
            string.IsNullOrWhiteSpace(appName) ? "Notificação" : appName,
            title,
            body,
            notification.CreationTime,
            await GetLogoAsync(appInfo));
    }

    private async Task<byte[]?> GetLogoAsync(global::Windows.ApplicationModel.AppInfo? appInfo)
    {
        if (appInfo?.AppUserModelId is not { } appId)
        {
            return null;
        }

        if (_logoCache.TryGetValue(appId, out var cached))
        {
            return cached;
        }

        byte[]? logo = null;
        try
        {
            logo = await WinRtStreams.ReadAllBytesAsync(appInfo.DisplayInfo.GetLogo(LogoSize));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Ícone de {App} indisponível.", appId);
        }

        _logoCache[appId] = logo;
        return logo;
    }
}
