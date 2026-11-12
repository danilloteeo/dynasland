using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;

namespace Dynasland.Modules.KeyLocks;

/// <summary>Avisa na ilha quando o Caps Lock ou o Num Lock é ligado ou desligado. Não lê o que é digitado.</summary>
public sealed class KeyLockModule(IKeyLockService keys, IEventBus bus) : IIslandModule
{
    public const string ModuleId = "keys";
    public const string ActivityId = "keys.state";

    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(1.8);

    public string Id => ModuleId;

    public string DisplayName => "Caps Lock e Num Lock";

    public string Description => "Aviso rápido ao ligar ou desligar o Caps Lock e o Num Lock. Só lê o estado dessas duas teclas.";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        keys.Changed += OnChanged;
        keys.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        keys.Changed -= OnChanged;
        keys.Stop();
        return Task.CompletedTask;
    }

    private void OnChanged(object? sender, KeyLockChangedEventArgs e) =>
        bus.PostActivity(new IslandActivity($"{ActivityId}.{e.Key}", ModuleId, ActivityKind.Transient, new KeyLockContent(e.Key, e.IsOn))
        {
            Priority = ActivityPriority.High,
            Duration = DisplayDuration,
            GroupKey = ActivityId,
        });
}
