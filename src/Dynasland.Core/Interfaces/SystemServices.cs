using Dynasland.Core.Models;

namespace Dynasland.Core.Interfaces;

// Contratos das integrações com o Windows consumidas pelos módulos.
// Eventos podem ser disparados em qualquer thread.

/// <summary>Todas as sessões de mídia do Windows (música, vídeos no navegador...), simultâneas.</summary>
public interface IMediaSessionService
{
    /// <summary>Sessões com mídia carregada; a sessão atual do Windows vem primeiro.</summary>
    IReadOnlyList<MediaSnapshot> Sessions { get; }

    /// <summary>Sessão que o Windows considera atual (teclas de mídia agem nela); <c>null</c> se nenhuma.</summary>
    string? CurrentSessionId { get; }

    /// <summary>Alguma sessão mudou, surgiu ou terminou.</summary>
    event EventHandler? Changed;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    Task<bool> PlayAsync(string sessionId);

    Task<bool> PauseAsync(string sessionId);

    Task<bool> SkipNextAsync(string sessionId);

    Task<bool> SkipPreviousAsync(string sessionId);
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
