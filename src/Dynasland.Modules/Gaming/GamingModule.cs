using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Gaming;

/// <summary>Métricas do jogo em andamento (compacto e painel).</summary>
public sealed partial class GameContent : ObservableObject, IActivityContent
{
    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    private string _fpsText = "--";

    [ObservableProperty]
    private bool _hasFps;

    [ObservableProperty]
    private string _cpuText = "--";

    [ObservableProperty]
    private string _gpuText = "--";

    [ObservableProperty]
    private string _memoryText = "--";

    [ObservableProperty]
    private string _videoMemoryText = "indisponível";

    [ObservableProperty]
    private string _temperatureText = "indisponível";

    [ObservableProperty]
    private string _frequencyText = "indisponível";

    [ObservableProperty]
    private string _sessionText = string.Empty;

    [ObservableProperty]
    private string? _fpsHint;

    public string Glyph => Glyphs.Game;

    /// <summary>Resumo de uma linha para o compacto.</summary>
    public string CompactText => HasFps ? $"{FpsText} FPS · CPU {CpuText}" : $"CPU {CpuText} · GPU {GpuText}";

    partial void OnFpsTextChanged(string value) => OnPropertyChanged(nameof(CompactText));

    partial void OnCpuTextChanged(string value) => OnPropertyChanged(nameof(CompactText));

    partial void OnGpuTextChanged(string value) => OnPropertyChanged(nameof(CompactText));

    public void Apply(SystemMetrics metrics, double? fps, TimeSpan playing)
    {
        HasFps = fps is not null;
        FpsText = fps is { } value ? Math.Round(value).ToString(CultureInfo.InvariantCulture) : "--";
        CpuText = metrics.CpuPercent is { } cpu ? $"{cpu:0}%" : "--";
        GpuText = metrics.GpuPercent is { } gpu ? $"{gpu:0}%" : "--";
        MemoryText = $"{metrics.Memory.Percent:0}%";
        VideoMemoryText = metrics.VideoMemoryUsedBytes is { } used
            ? metrics.VideoMemoryTotalBytes is { } total and > 0
                ? $"{used / 1073741824.0:0.0} de {total / 1073741824.0:0.0} GB"
                : $"{used / 1073741824.0:0.0} GB"
            : "indisponível";
        var temperatures = new List<string>();
        if (metrics.CpuTemperatureCelsius is { } cpuTemp)
        {
            temperatures.Add($"CPU {cpuTemp:0} °C");
        }

        if (metrics.GpuTemperatureCelsius is { } gpuTemp)
        {
            temperatures.Add($"GPU {gpuTemp:0} °C");
        }

        TemperatureText = temperatures.Count > 0 ? string.Join(" · ", temperatures) : "indisponível neste computador";
        FrequencyText = metrics.CpuFrequencyMhz is { } mhz ? $"{mhz / 1000:0.00} GHz" : "indisponível";
        SessionText = $"Jogando há {TextFormat.Duration(playing)}";
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Compact => new IslandDimensions(240, 34, 17),
        IslandState.Expanded => new IslandDimensions(320, 60, 30),
        IslandState.Interactive => new IslandDimensions(400, FpsHint is null ? 176 : 200, 34),
        _ => null,
    };
}

/// <summary>
/// Gaming Monitor: detecta jogos, ativa o modo gamer (silencia notificações comuns e, opcionalmente,
/// oculta a ilha) e mostra métricas do jogo — FPS quando o Windows permite medir, CPU, GPU, memória,
/// memória de vídeo, frequência e temperaturas disponíveis.
/// </summary>
public sealed class GamingModule(
    IGameDetector detector,
    IFpsProvider fps,
    ISystemMetricsService metrics,
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IModuleOptions options,
    ILogger<GamingModule> logger) : IIslandModule
{
    public const string ModuleId = "gaming";
    public const string ActivityId = "gaming.session";

    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);

    public static readonly ModuleOption ShowMetrics = ModuleOption.Toggle(
        "showMetrics", "Mostrar métricas durante o jogo", "O compacto mostra FPS (ou CPU/GPU) enquanto você joga. Com \"Ocultar em tela cheia\" ligado, a ilha some em jogos de tela cheia.");

    public static readonly ModuleOption MuteNotifications = ModuleOption.Toggle(
        "muteNotifications", "Silenciar notificações comuns", "Durante o jogo, só notificações de prioridade alta aparecem; as outras ficam no histórico.");

    public static readonly ModuleOption HideIsland = ModuleOption.Toggle(
        "hideIsland", "Ocultar a ilha durante jogos", "Esconde a ilha por completo enquanto um jogo estiver em primeiro plano.", defaultValue: false);

    public static readonly ModuleOption MeasureFps = ModuleOption.Toggle(
        "measureFps", "Medir FPS", "Usa os eventos de desempenho do DirectX (sem injetar nada no jogo). Exige administrador ou o grupo \"Usuários de log de desempenho\".");

    private readonly Lock _lock = new();
    private GameContent? _content;
    private GameSession? _game;
    private ITimer? _sampler;
    private bool _fpsRunning;

    public string Id => ModuleId;

    public string DisplayName => "Gaming Monitor";

    public string Description => "Detecta jogos, ativa o modo gamer e mostra FPS e métricas de hardware disponíveis.";

    public IReadOnlyList<ModuleOption> Options => [ShowMetrics, MuteNotifications, HideIsland, MeasureFps];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _content = new GameContent();
        detector.Changed += OnGameChanged;
        options.Changed += OnOptionsChanged;
        detector.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        detector.Changed -= OnGameChanged;
        options.Changed -= OnOptionsChanged;
        detector.Stop();
        EndSession();
        return Task.CompletedTask;
    }

    private void OnGameChanged(object? sender, GameSession? game)
    {
        if (game is null)
        {
            EndSession();
        }
        else
        {
            BeginSession(game);
        }
    }

    private void OnOptionsChanged(object? sender, (string ModuleId, string Key) e)
    {
        if (e.ModuleId == ModuleId && _game is { } game)
        {
            BeginSession(game);
        }
    }

    private void BeginSession(GameSession game)
    {
        if (_content is not { } content)
        {
            return;
        }

        lock (_lock)
        {
            _game = game;
            if (options.IsOn(ModuleId, MeasureFps) && !_fpsRunning)
            {
                _fpsRunning = fps.Start();
            }

            _sampler ??= time.CreateTimer(_ => Sample(), null, TimeSpan.Zero, SampleInterval);
        }

        var hint = options.IsOn(ModuleId, MeasureFps) ? fps.UnavailableReason : null;
        ui.Post(() =>
        {
            content.GameName = game.Name;
            content.FpsHint = hint;
        });
        logger.LogInformation("Modo gamer: {Game}.", game.Name);
        bus.Publish(new GamingModeChangedEvent(true, game, options.IsOn(ModuleId, MuteNotifications), options.IsOn(ModuleId, HideIsland)));

        if (options.IsOn(ModuleId, ShowMetrics))
        {
            bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Live, content) { Priority = ActivityPriority.Normal });
        }
        else
        {
            bus.RemoveActivity(ActivityId);
        }
    }

    private void EndSession()
    {
        bool wasActive;
        lock (_lock)
        {
            wasActive = _game is not null;
            _game = null;
            _sampler?.Dispose();
            _sampler = null;
            if (_fpsRunning)
            {
                fps.Stop();
                _fpsRunning = false;
            }
        }

        if (wasActive)
        {
            bus.RemoveActivity(ActivityId);
            bus.Publish(new GamingModeChangedEvent(false, null, false, false));
        }
    }

    private void Sample()
    {
        GameSession? game;
        lock (_lock)
        {
            game = _game;
        }

        if (game is null || _content is not { } content)
        {
            return;
        }

        try
        {
            var sample = metrics.Sample();
            var frames = _fpsRunning ? fps.GetFps(game.ProcessId) : null;
            var playing = time.GetUtcNow() - game.StartedAt;
            ui.Post(() => content.Apply(sample, frames, playing));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Falha ao ler métricas do jogo.");
        }
    }
}
