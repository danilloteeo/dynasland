using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;

namespace Dynasland.Modules.Brightness;

/// <summary>
/// Brilho da tela integrada. Em computadores sem controle de brilho (ex.: desktops com
/// monitores externos) o módulo fica indisponível em vez de exibir um controle sem efeito.
/// </summary>
public sealed class BrightnessModule(IBrightnessService brightness, IEventBus bus, IUiDispatcher ui, TimeProvider time) : IIslandModule
{
    public const string ModuleId = "brightness";
    public const string ActivityId = "brightness.level";

    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(2.5);

    // Eventos que chegam logo após um ajuste feito pela ilha são eco desse ajuste.
    private static readonly TimeSpan OwnChangeWindow = TimeSpan.FromSeconds(1.5);

    private readonly Lock _lock = new();
    private BrightnessContent? _content;
    private DateTimeOffset _lastOwnChange = DateTimeOffset.MinValue;
    private int? _pendingLevel;
    private bool _applying;

    public string Id => ModuleId;

    public string DisplayName => "Brilho";

    public string Description => "Mostra e controla o brilho da tela integrada (notebooks e tablets).";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!await brightness.StartAsync(cancellationToken))
        {
            throw new ModuleUnavailableException("O brilho desta tela não pode ser controlado pelo Windows.");
        }

        var content = new BrightnessContent(RequestBrightness);
        _content = content;
        brightness.Changed += OnBrightnessChanged;

        var initial = brightness.Current ?? 0;
        ui.Post(() => content.Apply(initial));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        brightness.Changed -= OnBrightnessChanged;
        await brightness.StopAsync();
        _content = null;
    }

    private void OnBrightnessChanged(object? sender, int percent)
    {
        if (_content is not { } content)
        {
            return;
        }

        ui.Post(() => content.Apply(percent));

        bool isEcho;
        lock (_lock)
        {
            isEcho = time.GetUtcNow() - _lastOwnChange < OwnChangeWindow;
        }

        if (!isEcho)
        {
            bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Transient, content)
            {
                Priority = ActivityPriority.High,
                Duration = DisplayDuration,
            });
        }
    }

    /// <summary>
    /// Aplica o brilho pedido pelo slider. WMI é lento; enquanto uma chamada está em andamento,
    /// só o valor mais recente é guardado e enviado em seguida.
    /// </summary>
    private void RequestBrightness(int percent)
    {
        lock (_lock)
        {
            _lastOwnChange = time.GetUtcNow();
            _pendingLevel = percent;
            if (_applying)
            {
                return;
            }

            _applying = true;
        }

        _ = Task.Run(async () =>
        {
            while (true)
            {
                int level;
                lock (_lock)
                {
                    if (_pendingLevel is not { } next)
                    {
                        _applying = false;
                        return;
                    }

                    level = next;
                    _pendingLevel = null;
                    _lastOwnChange = time.GetUtcNow();
                }

                await brightness.SetAsync(level);
            }
        });
    }
}
