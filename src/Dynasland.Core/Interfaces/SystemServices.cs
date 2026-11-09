using Dynasland.Core.Models;

namespace Dynasland.Core.Interfaces;

// Contratos das integrações com o Windows consumidas pelos módulos.
// Eventos podem ser disparados em qualquer thread.

public interface IMediaSessionService
{
    MediaSnapshot? Current { get; }

    /// <summary>Nova sessão, faixa, estado ou posição; <c>null</c> quando não há sessão.</summary>
    event EventHandler<MediaSnapshot?>? Changed;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    Task<bool> PlayAsync();

    Task<bool> PauseAsync();

    Task<bool> SkipNextAsync();

    Task<bool> SkipPreviousAsync();
}

public interface IAudioVolumeService
{
    VolumeState? Current { get; }

    event EventHandler<VolumeChangedEventArgs>? Changed;

    /// <returns><c>false</c> se não houver dispositivo de saída.</returns>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    void SetLevel(float level);

    void SetMuted(bool muted);
}

public interface IBrightnessService
{
    /// <summary>Brilho atual em porcentagem; <c>null</c> se não suportado.</summary>
    int? Current { get; }

    event EventHandler<int>? Changed;

    /// <returns><c>false</c> se nenhum monitor deste computador permite controlar o brilho.</returns>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    Task SetAsync(int percent);
}

public interface IPowerService
{
    PowerState Current { get; }

    event EventHandler<PowerState>? Changed;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}

public interface INotificationService
{
    event EventHandler<NotificationInfo>? Received;

    /// <summary>A notificação saiu da Central de Ações (dispensada no Windows ou aqui).</summary>
    event EventHandler<uint>? Removed;

    Task<NotificationAccess> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    void Dismiss(uint notificationId);

    /// <summary>Abre o aplicativo de origem, quando o Windows permitir.</summary>
    bool TryOpenApp(string appId);
}
