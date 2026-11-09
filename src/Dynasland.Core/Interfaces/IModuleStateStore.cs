namespace Dynasland.Core.Interfaces;

/// <summary>Persistência de quais módulos o usuário ativou ou desativou.</summary>
public interface IModuleStateStore
{
    Task<IReadOnlyDictionary<string, bool>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SetEnabledAsync(string moduleId, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Esquece as escolhas do usuário (todos voltam ao padrão).</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
