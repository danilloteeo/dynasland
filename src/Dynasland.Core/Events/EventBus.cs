using Microsoft.Extensions.Logging;

namespace Dynasland.Core.Events;

public sealed class EventBus(ILogger<EventBus> logger) : IEventBus
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Type, Delegate[]> _handlers = [];

    public void Publish<TEvent>(TEvent @event) where TEvent : notnull
    {
        Delegate[]? handlers;
        lock (_lock)
        {
            _handlers.TryGetValue(typeof(TEvent), out handlers);
        }

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                ((Action<TEvent>)handler)(@event);
            }
            catch (Exception ex)
            {
                // Um assinante com defeito não pode impedir os demais de receber o evento.
                logger.LogError(ex, "Falha ao processar o evento {Event}.", typeof(TEvent).Name);
            }
        }
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock)
        {
            _handlers[typeof(TEvent)] = _handlers.TryGetValue(typeof(TEvent), out var current)
                ? [.. current, handler]
                : [handler];
        }

        return new Subscription(() => Unsubscribe(typeof(TEvent), handler));
    }

    private void Unsubscribe(Type eventType, Delegate handler)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventType, out var current))
            {
                return;
            }

            var remaining = current.Where(h => !ReferenceEquals(h, handler)).ToArray();
            if (remaining.Length == 0)
            {
                _handlers.Remove(eventType);
            }
            else
            {
                _handlers[eventType] = remaining;
            }
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
