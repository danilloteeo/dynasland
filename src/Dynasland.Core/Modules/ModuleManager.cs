using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dynasland.Core.Modules;

/// <summary>
/// Inicia, para e ativa/desativa módulos, isolando falhas: um módulo que falha
/// ao iniciar é marcado com erro e não afeta os demais.
/// </summary>
public sealed class ModuleManager : IDisposable
{
    private readonly IReadOnlyList<Slot> _slots;
    private readonly IModuleStateStore _store;
    private readonly IEventBus _bus;
    private readonly ILogger<ModuleManager> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ModuleManager(IEnumerable<IIslandModule> modules, IModuleStateStore store, IEventBus bus, ILogger<ModuleManager> logger)
    {
        _slots = modules.Select(m => new Slot(m)).ToList();
        _store = store;
        _bus = bus;
        _logger = logger;

        var duplicated = _slots.GroupBy(s => s.Module.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicated is not null)
        {
            throw new InvalidOperationException($"Módulo registrado mais de uma vez: {duplicated.Key}.");
        }
    }

    /// <summary>Disparado após qualquer mudança de estado de um módulo. Pode ocorrer fora da thread de UI.</summary>
    public event EventHandler? ModulesChanged;

    public IReadOnlyList<ModuleInfo> GetModules() => _slots
        .Select(s => new ModuleInfo(s.Module.Id, s.Module.DisplayName, s.Module.Description, s.IsEnabled, s.IsRunning, s.Error))
        .ToList();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var states = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var slot in _slots)
            {
                slot.IsEnabled = states.TryGetValue(slot.Module.Id, out var enabled) ? enabled : slot.Module.EnabledByDefault;
                if (slot.IsEnabled)
                {
                    await StartSlotAsync(slot, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        ModulesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetEnabledAsync(string moduleId, bool enabled, CancellationToken cancellationToken = default)
    {
        var slot = _slots.FirstOrDefault(s => s.Module.Id == moduleId)
            ?? throw new ArgumentException($"Módulo desconhecido: {moduleId}.", nameof(moduleId));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _store.SetEnabledAsync(moduleId, enabled, cancellationToken).ConfigureAwait(false);
            slot.IsEnabled = enabled;

            if (enabled && !slot.IsRunning)
            {
                await StartSlotAsync(slot, cancellationToken).ConfigureAwait(false);
            }
            else if (!enabled && slot.IsRunning)
            {
                await StopSlotAsync(slot, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }

        ModulesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var slot in _slots.Where(s => s.IsRunning).Reverse())
            {
                await StopSlotAsync(slot, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StartSlotAsync(Slot slot, CancellationToken cancellationToken)
    {
        try
        {
            await slot.Module.StartAsync(cancellationToken).ConfigureAwait(false);
            slot.IsRunning = true;
            slot.Error = null;
            _logger.LogInformation("Módulo {Module} iniciado.", slot.Module.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            slot.IsRunning = false;
            slot.Error = ex.Message;
            _logger.LogError(ex, "Falha ao iniciar o módulo {Module}.", slot.Module.Id);
            await TryStopAsync(slot, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task StopSlotAsync(Slot slot, CancellationToken cancellationToken)
    {
        await TryStopAsync(slot, cancellationToken).ConfigureAwait(false);
        slot.IsRunning = false;
        _logger.LogInformation("Módulo {Module} parado.", slot.Module.Id);
    }

    /// <summary>Para o módulo (ignorando falhas) e retira suas atividades da ilha.</summary>
    private async Task TryStopAsync(Slot slot, CancellationToken cancellationToken)
    {
        try
        {
            await slot.Module.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao parar o módulo {Module}.", slot.Module.Id);
        }
        finally
        {
            _bus.Publish(new ModuleStoppedEvent(slot.Module.Id));
        }
    }

    public void Dispose() => _gate.Dispose();

    private sealed class Slot(IIslandModule module)
    {
        public IIslandModule Module { get; } = module;

        public bool IsEnabled { get; set; }

        public bool IsRunning { get; set; }

        public string? Error { get; set; }
    }
}
