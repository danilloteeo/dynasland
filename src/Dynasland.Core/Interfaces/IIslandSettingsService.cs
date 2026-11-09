using Dynasland.Core.Models;

namespace Dynasland.Core.Interfaces;

public interface IIslandSettingsService
{
    IslandSettings Current { get; }

    /// <summary>Disparado após uma alteração ser persistida. Pode ocorrer fora da thread de UI.</summary>
    event EventHandler<IslandSettings>? Changed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(Func<IslandSettings, IslandSettings> update, CancellationToken cancellationToken = default);
}
