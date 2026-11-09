using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;

namespace Dynasland.Core.Services;

public sealed class IslandSettingsService(ISettingsStore store) : IIslandSettingsService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public IslandSettings Current { get; private set; } = IslandSettings.Default;

    public event EventHandler<IslandSettings>? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var values = await store.GetAllAsync(cancellationToken);
        Current = IslandSettingsMapper.FromValues(values);
    }

    public async Task UpdateAsync(Func<IslandSettings, IslandSettings> update, CancellationToken cancellationToken = default)
    {
        IslandSettings updated;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            updated = update(Current);
            if (updated == Current)
            {
                return;
            }

            await store.SetManyAsync(IslandSettingsMapper.ToValues(updated), cancellationToken);
            Current = updated;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, updated);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await store.ClearAsync(cancellationToken);
            Current = IslandSettings.Default;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, IslandSettings.Default);
    }

    public void Dispose() => _gate.Dispose();
}
