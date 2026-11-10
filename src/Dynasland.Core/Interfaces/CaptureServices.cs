using Dynasland.Core.Models;

namespace Dynasland.Core.Interfaces;

// Capture Center: capturas de tela e gravação. Coordenadas sempre em pixels físicos da tela.

/// <summary>Imagem capturada, já codificada em PNG.</summary>
public sealed record CapturedImage(byte[] Png, int Width, int Height);

/// <summary>Janela visível que pode ser capturada ou gravada.</summary>
public sealed record CaptureWindowInfo(IntPtr Handle, string Title, string ProcessName);

public interface IScreenshotService
{
    /// <summary>Tela inteira de um monitor (nome do dispositivo) ou de todos (<c>null</c>).</summary>
    Task<CapturedImage> CaptureScreenAsync(string? monitorDeviceName = null);

    /// <summary>Área da tela (pixels físicos, coordenadas da área de trabalho virtual).</summary>
    Task<CapturedImage> CaptureRegionAsync(PixelRect region);

    /// <summary>Uma janela, mesmo parcialmente coberta (inclui conteúdo acelerado por GPU).</summary>
    Task<CapturedImage> CaptureWindowAsync(IntPtr window);

    /// <summary>Janela em primeiro plano agora (a ilha nunca rouba o foco, então é a do usuário).</summary>
    IntPtr GetForegroundWindow();

    IReadOnlyList<CaptureWindowInfo> GetWindows();
}

public enum RecordingTargetKind
{
    Monitor,
    Window,
}

/// <param name="MonitorDeviceName">Para <see cref="RecordingTargetKind.Monitor"/>; <c>null</c> = monitor principal.</param>
public sealed record RecordingTarget(RecordingTargetKind Kind, string? MonitorDeviceName = null, IntPtr Window = default);

/// <param name="FramesPerSecond">30 ou 60.</param>
/// <param name="VideoBitrate">Bits por segundo do vídeo H.264.</param>
public sealed record RecordingOptions(
    RecordingTarget Target,
    string OutputPath,
    int FramesPerSecond,
    uint VideoBitrate,
    bool SystemAudio,
    bool Microphone,
    bool ShowCursor);

public enum RecordingState
{
    Idle,
    Starting,
    Recording,
    Paused,
    Stopping,
}

/// <summary>Resultado de uma gravação encerrada.</summary>
public sealed record RecordingResult(bool Success, string? Path, TimeSpan Duration, long SizeBytes, string? Error);

/// <summary>Gravação de tela/janela em MP4 (H.264 + AAC), com áudio do sistema e microfone opcionais.</summary>
public interface IScreenRecorder
{
    /// <summary>A gravação é suportada neste Windows (Windows.Graphics.Capture, Windows 10 1903+).</summary>
    bool IsSupported { get; }

    RecordingState State { get; }

    /// <summary>Tempo gravado (sem as pausas).</summary>
    TimeSpan Elapsed { get; }

    /// <summary>Mudou de estado (qualquer thread).</summary>
    event EventHandler<RecordingState>? StateChanged;

    /// <summary>Gravação terminou (normalmente ou por erro). Qualquer thread.</summary>
    event EventHandler<RecordingResult>? Completed;

    /// <exception cref="InvalidOperationException">Já existe uma gravação ou o alvo não pode ser gravado (mensagem para o usuário).</exception>
    Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default);

    void Pause();

    void Resume();

    /// <summary>Finaliza o arquivo; o resultado chega em <see cref="Completed"/>.</summary>
    Task StopAsync();
}

/// <summary>Seleção de uma área da tela pelo usuário (sobreposição com a tela congelada).</summary>
public interface IRegionSelector
{
    /// <param name="background">Captura da área de trabalho virtual inteira, exibida durante a seleção.</param>
    /// <param name="desktop">Área de trabalho virtual em pixels físicos (posição da captura).</param>
    /// <returns>Área escolhida em pixels físicos; <c>null</c> se cancelada (Esc).</returns>
    Task<PixelRect?> SelectAsync(CapturedImage background, PixelRect desktop);
}
