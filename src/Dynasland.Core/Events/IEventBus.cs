namespace Dynasland.Core.Events;

/// <summary>
/// Barramento de eventos em memória para comunicação entre módulos e a interface.
/// Os manipuladores são chamados de forma síncrona na thread de quem publica.
/// </summary>
public interface IEventBus
{
    void Publish<TEvent>(TEvent @event) where TEvent : notnull;

    /// <returns>Descarte o retorno para cancelar a inscrição.</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;
}
