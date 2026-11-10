using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Brightness;

/// <summary>
/// Brilho da tela integrada (WMI, com aviso quando muda) e de monitores externos (DDC/CI, ajustados
/// pelo painel ou pelos atalhos). Sem nenhuma tela controlável, o módulo fica indisponível em vez de
/// exibir controles sem efeito.
/// </summary>
public sealed class BrightnessModule(
    IBrightnessService brightness,
    IExternalBrightnessService external,
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IModuleOptions options,
    IShortcutRegistry shortcuts,
    IGlobalShortcutRegistry globalShortcuts) : IIslandModule
{
    public const string ModuleId = "brightness";
    public const string ActivityId = "brightness.level";
    public const string PanelActivityId = "brightness.panel";
    public const string IntegratedId = "integrated";

    private const int ShortcutStep = 10;
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(2.5);

    // Eventos que chegam logo após um ajuste feito pela ilha são eco desse ajuste.
    private static readonly TimeSpan OwnChangeWindow = TimeSpan.FromSeconds(1.5);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, (int? Pending, bool Applying)> _writes = [];
    private readonly List<IDisposable> _registrations = [];
    private BrightnessContent? _integrated;
    private BrightnessPanelContent? _panel;
    private DateTimeOffset _lastOwnChange = DateTimeOffset.MinValue;
    private bool _hasIntegrated;

    public string Id => ModuleId;

    public string DisplayName => "Brilho";

    public string Description => "Brilho da tela do notebook e de monitores externos compatíveis com DDC/CI.";

    public static readonly ModuleOption ShowOnChange = ModuleOption.Toggle(
        "showOnChange", "Mostrar ao mudar o brilho", "Expande a ilha quando o brilho da tela integrada é alterado pelas teclas ou pelo Windows.");

    public IReadOnlyList<ModuleOption> Options => [ShowOnChange];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _hasIntegrated = await brightness.StartAsync(cancellationToken);
        var displays = await external.GetDisplaysAsync();
        if (!_hasIntegrated && displays.Count == 0)
        {
            throw new ModuleUnavailableException("Nenhuma tela com brilho controlável: a tela não é integrada e os monitores não respondem ao DDC/CI.");
        }

        _panel = new BrightnessPanelContent(RefreshExternalAsync);
        if (_hasIntegrated)
        {
            var integrated = new BrightnessContent(percent => RequestBrightness(IntegratedId, percent)) { Name = "Tela integrada" };
            _integrated = integrated;
            brightness.Changed += OnBrightnessChanged;
            var initial = brightness.Current ?? 0;
            ui.Post(() => integrated.Apply(initial));
        }

        ApplyExternal(displays);
        _registrations.Add(shortcuts.Register(new IslandShortcut("brightness.panel", ModuleId, Glyphs.Brightness, "Brilho", OpenPanel)));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut("brightness.up", "Brilho: aumentar", "Telas", null, () => _ = StepAsync(+ShortcutStep))));
        _registrations.Add(globalShortcuts.Register(new GlobalShortcut("brightness.down", "Brilho: diminuir", "Telas", null, () => _ = StepAsync(-ShortcutStep))));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
        brightness.Changed -= OnBrightnessChanged;
        await brightness.StopAsync();
        _integrated = null;
        _panel = null;
    }

    public void OpenPanel()
    {
        if (_panel is { } panel)
        {
            bus.PostActivity(new IslandActivity(PanelActivityId, ModuleId, ActivityKind.Transient, panel) { OpenInteractive = true });
        }
    }

    private void OnBrightnessChanged(object? sender, int percent)
    {
        if (_integrated is not { } content)
        {
            return;
        }

        ui.Post(() => content.Apply(percent));

        bool isEcho;
        lock (_lock)
        {
            isEcho = time.GetUtcNow() - _lastOwnChange < OwnChangeWindow;
        }

        if (!isEcho && options.IsOn(ModuleId, ShowOnChange))
        {
            bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Transient, content)
            {
                Priority = ActivityPriority.High,
                Duration = DisplayDuration,
            });
        }
    }

    /// <summary>Atalho: muda o brilho de todas as telas controláveis e mostra o nível na ilha.</summary>
    private async Task StepAsync(int delta)
    {
        if (_panel is not { } panel)
        {
            return;
        }

        await RefreshExternalAsync();
        var targets = panel.Displays.ToList();
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var display in targets)
        {
            var level = Math.Clamp(display.Percent + delta, 0, 100);
            ui.Post(() => display.Apply(level));
            RequestBrightness(display.DisplayId, level);
        }

        bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Transient, targets[0])
        {
            Priority = ActivityPriority.High,
            Duration = DisplayDuration,
        });
    }

    /// <summary>Monitores externos não avisam mudanças: relê os valores quando o painel abre.</summary>
    private async Task RefreshExternalAsync() => ApplyExternal(await external.GetDisplaysAsync());

    private void ApplyExternal(IReadOnlyList<ExternalDisplayBrightness> displays)
    {
        if (_panel is not { } panel)
        {
            return;
        }

        var integrated = _integrated;
        ui.Post(() => panel.SetDisplays(integrated, displays, id => percent => RequestBrightness(id, percent)));
    }

    /// <summary>
    /// Aplica o brilho pedido. WMI e DDC/CI são lentos; enquanto uma escrita está em andamento numa tela,
    /// só o valor mais recente é guardado e enviado em seguida.
    /// </summary>
    private void RequestBrightness(string displayId, int percent)
    {
        lock (_lock)
        {
            _lastOwnChange = time.GetUtcNow();
            var state = _writes.GetValueOrDefault(displayId);
            _writes[displayId] = (percent, state.Applying);
            if (state.Applying)
            {
                return;
            }

            _writes[displayId] = (percent, true);
        }

        _ = Task.Run(async () =>
        {
            while (true)
            {
                int level;
                lock (_lock)
                {
                    var state = _writes[displayId];
                    if (state.Pending is not { } next)
                    {
                        _writes[displayId] = (null, false);
                        return;
                    }

                    level = next;
                    _writes[displayId] = (null, true);
                    _lastOwnChange = time.GetUtcNow();
                }

                if (displayId == IntegratedId)
                {
                    await brightness.SetAsync(level);
                }
                else
                {
                    await external.SetAsync(displayId, level);
                }
            }
        });
    }
}
