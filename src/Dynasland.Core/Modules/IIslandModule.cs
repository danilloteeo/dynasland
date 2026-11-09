namespace Dynasland.Core.Modules;

/// <summary>
/// Funcionalidade independente da ilha (mídia, volume, relógio...). Um módulo nunca acessa a janela:
/// comunica-se publicando atividades no <see cref="Events.IEventBus"/>.
/// </summary>
public interface IIslandModule
{
    /// <summary>Identificador estável, usado para persistir se o módulo está ativo.</summary>
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    bool EnabledByDefault => true;

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
