using System.Globalization;
using System.IO;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Capture;

/// <summary>
/// Capture Center: capturas de tela (região, tela inteira, janela ativa, com temporizador opcional) e
/// gravação de tela em MP4 com áudio. Cada resultado é salvo na pasta configurada, entra no histórico da
/// Central e aparece na ilha com as ações de copiar, abrir e mostrar na pasta.
/// </summary>
public sealed class CaptureModule(
    IScreenshotService screenshots,
    IScreenRecorder recorder,
    IRegionSelector regionSelector,
    ICaptureHistoryStore history,
    IClipboardService clipboard,
    IShellService shell,
    IMonitorService monitors,
    IHubService hub,
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IModuleOptions options,
    IShortcutRegistry shortcuts,
    IGlobalShortcutRegistry globalShortcuts,
    ILogger<CaptureModule> logger) : IIslandModule
{
    public const string ModuleId = "capture";
    public const string PanelActivityId = "capture.panel";
    public const string ResultActivityId = "capture.result";
    public const string RecordingActivityId = "capture.recording";
    public const string StatusActivityId = "capture.status";

    private const int ThumbnailWidth = 120;

    public static readonly ModuleOption CopyToClipboard = ModuleOption.Toggle(
        "copyToClipboard", "Copiar capturas automaticamente", "Toda captura de tela também vai para a área de transferência.");

    public static readonly ModuleOption Delay = ModuleOption.Slider(
        "delay", "Temporizador da captura", "Espera antes de capturar a tela inteira ou a janela (0 = na hora).", 0, 0, 10, 1, "s");

    public static readonly ModuleOption RecordingFps = ModuleOption.Slider(
        "fps", "Quadros por segundo da gravação", "60 deixa movimentos mais suaves; 30 gera arquivos menores e pesa menos no computador.", 30, 30, 60, 30, "fps");

    public static readonly ModuleOption RecordingQuality = ModuleOption.Slider(
        "bitrate", "Qualidade da gravação", "Taxa de bits do vídeo: mais alta, melhor imagem e arquivo maior.", 8, 2, 30, 1, "Mbps");

    public static readonly ModuleOption RecordSystemAudio = ModuleOption.Toggle(
        "systemAudio", "Gravar áudio do sistema", "Inclui o que está tocando no computador.");

    public static readonly ModuleOption RecordMicrophone = ModuleOption.Toggle(
        "microphone", "Gravar o microfone", "Mistura a voz do microfone padrão à gravação.", defaultValue: false);

    public static readonly ModuleOption RecordCursor = ModuleOption.Toggle(
        "cursor", "Mostrar o cursor na gravação", "Desligado, o ponteiro do mouse não aparece no vídeo.");

    private readonly List<IDisposable> _registrations = [];
    private CapturePanelContent? _panel;
    private RecordingContent? _recording;
    private StatusContent? _status;
    private ITimer? _recordingTicker;
    private string _recordingTarget = string.Empty;
    private int _capturing;

    public string Id => ModuleId;

    public string DisplayName => "Capture Center";

    public string Description => "Capturas de tela (região, tela, janela) e gravação de tela com áudio, salvas em Imagens e Vídeos.";

    public IReadOnlyList<ModuleOption> Options => [CopyToClipboard, Delay, RecordingFps, RecordingQuality, RecordSystemAudio, RecordMicrophone, RecordCursor];

    public static string PicturesFolder => Path.Combine(OutputRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Dynasland");

    public static string VideosFolder => Path.Combine(OutputRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Dynasland");

    /// <summary>Somente para testes: pasta base no lugar de Imagens/Vídeos do usuário.</summary>
    internal static string? OutputRoot { get; set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _panel = new CapturePanelContent(
            () => _ = CaptureRegionAsync(),
            () => _ = CaptureScreenAsync(),
            () => _ = CaptureWindowAsync(),
            () => _ = ToggleRecordingAsync(),
            () => hub.Open(HubPage.Captures));
        _recording = new RecordingContent(PauseResume, () => _ = recorder.StopAsync());
        _status = new StatusContent(StatusKind.Info, string.Empty);
        recorder.StateChanged += OnRecorderStateChanged;
        recorder.Completed += OnRecordingCompleted;

        _registrations.Add(shortcuts.Register(new IslandShortcut("capture.panel", ModuleId, Glyphs.Camera, "Capturar", OpenPanel)));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut("capture.region", "Capturar região", "Capture Center",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'S'), () => _ = CaptureRegionAsync())));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut("capture.screen", "Capturar a tela inteira", "Capture Center",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 'S'), () => _ = CaptureScreenAsync())));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut("capture.window", "Capturar a janela ativa", "Capture Center",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'W'), () => _ = CaptureWindowAsync())));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut("capture.record", "Gravar tela: iniciar/parar", "Capture Center",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'R'), () => _ = ToggleRecordingAsync())));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
        recorder.StateChanged -= OnRecorderStateChanged;
        recorder.Completed -= OnRecordingCompleted;
        await recorder.StopAsync();
        _recordingTicker?.Dispose();
    }

    public void OpenPanel()
    {
        if (_panel is { } panel)
        {
            ui.Post(() =>
            {
                panel.Message = recorder.IsSupported ? null : "A gravação exige Windows 10 versão 1903 ou mais recente.";
                panel.RecordLabel = recorder.State is RecordingState.Recording or RecordingState.Paused ? "Parar gravação" : "Gravar tela";
            });
            bus.PostActivity(new IslandActivity(PanelActivityId, ModuleId, ActivityKind.Transient, panel) { OpenInteractive = true });
        }
    }

    public async Task CaptureRegionAsync()
    {
        await RunCaptureAsync(async () =>
        {
            bus.RemoveActivity(PanelActivityId);
            await Task.Delay(250); // a ilha recolhe antes de congelar a tela
            var desktop = Desktop();
            var background = await screenshots.CaptureScreenAsync();
            var region = await regionSelector.SelectAsync(background, desktop);
            if (region is not { Width: > 1, Height: > 1 } area) // um clique sem arrastar cancela
            {
                return null;
            }

            var png = CaptureImages.CropPng(background.Png, area.Left - desktop.Left, area.Top - desktop.Top, area.Width, area.Height);
            return new CapturedImage(png, area.Width, area.Height);
        }, "Região");
    }

    public Task CaptureScreenAsync() => RunCaptureAsync(async () =>
    {
        await CountdownAsync();
        return await screenshots.CaptureScreenAsync();
    }, "Tela inteira");

    public Task CaptureWindowAsync()
    {
        // A ilha nunca rouba o foco: a janela em primeiro plano agora é a do usuário.
        var window = screenshots.GetForegroundWindow();
        return RunCaptureAsync(async () =>
        {
            await CountdownAsync();
            return await screenshots.CaptureWindowAsync(window);
        }, "Janela");
    }

    public async Task ToggleRecordingAsync()
    {
        if (recorder.State is RecordingState.Recording or RecordingState.Paused)
        {
            await recorder.StopAsync();
            return;
        }

        if (recorder.State != RecordingState.Idle)
        {
            return;
        }

        bus.RemoveActivity(PanelActivityId);
        var fileName = $"Gravação {time.GetLocalNow():yyyy-MM-dd HH-mm-ss}.mp4";
        var monitor = RecordingMonitor();
        _recordingTarget = monitor?.DisplayNumber is { } number ? $"Monitor {number}" : "Tela";
        var recordingOptions = new RecordingOptions(
            new RecordingTarget(RecordingTargetKind.Monitor, monitor?.DeviceName),
            Path.Combine(VideosFolder, fileName),
            (int)options.Get(ModuleId, RecordingFps),
            (uint)(options.Get(ModuleId, RecordingQuality) * 1_000_000),
            options.IsOn(ModuleId, RecordSystemAudio),
            options.IsOn(ModuleId, RecordMicrophone),
            options.IsOn(ModuleId, RecordCursor));

        try
        {
            await recorder.StartAsync(recordingOptions);
        }
        catch (InvalidOperationException ex)
        {
            ShowStatus(StatusKind.Error, "Não foi possível gravar", ex.Message);
        }
    }

    private void PauseResume()
    {
        if (recorder.State == RecordingState.Paused)
        {
            recorder.Resume();
        }
        else
        {
            recorder.Pause();
        }
    }

    private async Task RunCaptureAsync(Func<Task<CapturedImage?>> capture, string kind)
    {
        if (Interlocked.Exchange(ref _capturing, 1) == 1)
        {
            return;
        }

        try
        {
            if (await capture() is not { } image)
            {
                return;
            }

            Directory.CreateDirectory(PicturesFolder);
            var path = UniquePath(Path.Combine(PicturesFolder, $"Captura {time.GetLocalNow():yyyy-MM-dd HH-mm-ss}.png"));
            await File.WriteAllBytesAsync(path, image.Png);
            await SaveHistoryAsync(new CaptureRecord(0, CaptureKind.Screenshot, path, time.GetUtcNow(), image.Png.LongLength, null));

            var copied = options.IsOn(ModuleId, CopyToClipboard) && await clipboard.SetImageAsync(image.Png);
            var png = image.Png;
            var thumbnail = ImageDecoder.Decode(png, ThumbnailWidth);
            var content = new CaptureResultContent(
                path,
                thumbnail,
                copied ? "Captura salva e copiada" : "Captura salva",
                $"{kind} · {image.Width} × {image.Height}",
                () => clipboard.SetImageAsync(png),
                () => shell.Open(path),
                () => shell.RevealInFolder(path));
            bus.PostActivity(new IslandActivity(ResultActivityId, ModuleId, ActivityKind.Transient, content) { Priority = ActivityPriority.High, Duration = TimeSpan.FromSeconds(6) });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha na captura de tela.");
            ShowStatus(StatusKind.Error, "Não foi possível capturar", ex.Message);
        }
        finally
        {
            Volatile.Write(ref _capturing, 0);
        }
    }

    private async Task CountdownAsync()
    {
        var seconds = (int)options.Get(ModuleId, Delay);
        bus.RemoveActivity(PanelActivityId);
        for (var remaining = seconds; remaining > 0; remaining--)
        {
            ShowStatus(StatusKind.Loading, $"Capturando em {remaining}…", null, TimeSpan.FromSeconds(1.2));
            await Task.Delay(TimeSpan.FromSeconds(1), time);
        }

        bus.RemoveActivity(StatusActivityId);

        // A ilha recolhe antes da captura para não aparecer na imagem.
        await Task.Delay(seconds > 0 ? 350 : 150);
    }

    private void OnRecorderStateChanged(object? sender, RecordingState state)
    {
        if (_recording is not { } content)
        {
            return;
        }

        switch (state)
        {
            case RecordingState.Recording or RecordingState.Paused:
                var paused = state == RecordingState.Paused;
                var target = _recordingTarget;
                ui.Post(() =>
                {
                    content.IsPaused = paused;
                    content.TargetText = target;
                });
                _recordingTicker ??= time.CreateTimer(_ => UpdateElapsed(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));

                // Gravação é contínua e não pode ser fechada pela ilha (pare pelo botão ou atalho).
                bus.PostActivity(new IslandActivity(RecordingActivityId, ModuleId, ActivityKind.Live, content)
                {
                    Priority = ActivityPriority.High,
                    ExpandOnStart = true,
                    CanDismiss = false,
                });
                break;

            case RecordingState.Idle:
                _recordingTicker?.Dispose();
                _recordingTicker = null;
                bus.RemoveActivity(RecordingActivityId);
                break;
        }
    }

    private void UpdateElapsed()
    {
        if (_recording is { } content)
        {
            var text = TextFormat.Timer(recorder.Elapsed);
            ui.Post(() => content.ElapsedText = text);
        }
    }

    private async void OnRecordingCompleted(object? sender, RecordingResult result)
    {
        try
        {
            if (!result.Success || result.Path is null)
            {
                ShowStatus(StatusKind.Error, "A gravação falhou", result.Error);
                return;
            }

            await SaveHistoryAsync(new CaptureRecord(0, CaptureKind.Recording, result.Path, time.GetUtcNow(), result.SizeBytes, result.Duration.TotalSeconds));
            var path = result.Path;
            var content = new CaptureResultContent(
                path,
                null,
                "Gravação salva",
                $"{TextFormat.Duration(result.Duration)} · {result.SizeBytes / (1024.0 * 1024):0.0} MB",
                null,
                () => shell.Open(path),
                () => shell.RevealInFolder(path));
            bus.PostActivity(new IslandActivity(ResultActivityId, ModuleId, ActivityKind.Transient, content) { Priority = ActivityPriority.High, Duration = TimeSpan.FromSeconds(8) });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao registrar a gravação.");
        }
    }

    private async Task SaveHistoryAsync(CaptureRecord record)
    {
        try
        {
            await history.AddAsync(record);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao guardar a captura no histórico.");
        }
    }

    private void ShowStatus(StatusKind kind, string title, string? message, TimeSpan? duration = null)
    {
        if (_status is not { } status)
        {
            return;
        }

        ui.Post(() =>
        {
            status.Kind = kind;
            status.Title = title;
            status.Message = message;
        });
        bus.PostActivity(new IslandActivity(StatusActivityId, ModuleId, ActivityKind.Transient, status)
        {
            Priority = ActivityPriority.High,
            Duration = duration ?? TimeSpan.FromSeconds(5),
        });
    }

    private PixelRect Desktop()
    {
        var all = monitors.GetMonitors();
        return all.Count == 0
            ? new PixelRect(0, 0, 1, 1)
            : new PixelRect(all.Min(m => m.Bounds.Left), all.Min(m => m.Bounds.Top), all.Max(m => m.Bounds.Right), all.Max(m => m.Bounds.Bottom));
    }

    /// <summary>A gravação é do monitor principal (outros monitores e janelas: ver RecordingTarget).</summary>
    private DisplayMonitor? RecordingMonitor()
    {
        var all = monitors.GetMonitors();
        return all.FirstOrDefault(m => m.IsPrimary) ?? all.FirstOrDefault();
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"{name} ({i}){extension}"));
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
