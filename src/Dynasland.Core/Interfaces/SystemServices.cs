using Dynasland.Core.Models;
using Dynasland.Core.Session;

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

/// <summary>Bloqueio de sessão e suspensão/retomada do computador.</summary>
public interface ISessionStateService
{
    /// <summary>Sessão bloqueada ou computador suspenso: a ilha deve ficar oculta.</summary>
    bool IsInterrupted { get; }

    /// <summary>Disparado na thread de UI.</summary>
    event EventHandler<SessionChangedEvent>? Changed;
}

/// <summary>Dispositivo de áudio (saída ou entrada) ativo.</summary>
public sealed record AudioDevice(string Id, string Name, bool IsDefault);

/// <summary>Microfone padrão: nível (0 a 1), mudo e nome.</summary>
public sealed record MicrophoneState(float Level, bool IsMuted, string? DeviceName);

/// <summary>Dispositivos de áudio: escolha da saída/entrada padrão e controle do microfone (Core Audio).</summary>
public interface IAudioDeviceService
{
    MicrophoneState? Microphone { get; }

    /// <summary>Nível, mudo ou dispositivo do microfone mudou (qualquer thread). <c>null</c> = sem microfone.</summary>
    event EventHandler<MicrophoneState?>? MicrophoneChanged;

    /// <summary>A lista de dispositivos ou o padrão mudou (qualquer thread).</summary>
    event EventHandler? DevicesChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    Task<IReadOnlyList<AudioDevice>> GetOutputDevicesAsync();

    Task<IReadOnlyList<AudioDevice>> GetInputDevicesAsync();

    /// <summary>Torna o dispositivo padrão para todas as funções (multimídia, console e comunicações).</summary>
    Task<bool> SetDefaultDeviceAsync(string deviceId);

    void SetMicrophoneLevel(float level);

    void SetMicrophoneMuted(bool muted);
}

/// <summary>Som de um aplicativo no dispositivo de saída padrão (todas as sessões do mesmo programa juntas).</summary>
/// <param name="Id">Identifica o programa (nome do processo).</param>
/// <param name="Level">Volume de 0 a 1, relativo ao volume geral.</param>
/// <param name="IsActive">Está tocando som agora.</param>
public sealed record AppAudioSession(string Id, string Name, float Level, bool IsMuted, bool IsActive);

/// <summary>Volume por aplicativo (misturador do Windows).</summary>
public interface IAppVolumeService
{
    Task<IReadOnlyList<AppAudioSession>> GetSessionsAsync();

    Task SetLevelAsync(string id, float level);

    Task SetMutedAsync(string id, bool muted);
}

/// <summary>
/// Indicadores de privacidade: microfone ou câmera em uso agora por algum aplicativo. Só consulta o
/// Windows enquanto a opção <see cref="IslandSettings.ShowPrivacyIndicators"/> está ligada; desligada, fica em <see cref="PrivacyUsage.None"/>.
/// </summary>
public interface IPrivacyIndicatorService
{
    PrivacyUsage Current { get; }

    /// <summary>O uso mudou (qualquer thread).</summary>
    event EventHandler<PrivacyUsage>? Changed;
}

/// <summary>Brilho de um monitor externo (DDC/CI), em porcentagem.</summary>
public sealed record ExternalDisplayBrightness(string Id, string Name, int Percent);

/// <summary>Monitores externos com brilho controlável por DDC/CI. Sem notificação de mudança: consultar ao exibir.</summary>
public interface IExternalBrightnessService
{
    Task<IReadOnlyList<ExternalDisplayBrightness>> GetDisplaysAsync();

    Task<bool> SetAsync(string displayId, int percent);
}
