namespace Dynasland.Core.Interfaces;

/// <summary>Armazenamento persistente de configurações em pares chave/valor.</summary>
public interface ISettingsStore
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SetManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default);
}
