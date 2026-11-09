using Dynasland.Core.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Core;

public class EventBusTests
{
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);

    private sealed record Ping(int Value);

    private sealed record Pong(string Text);

    [Fact]
    public void Delivers_only_to_subscribers_of_the_event_type()
    {
        var pings = new List<int>();
        var pongs = 0;
        using var a = _bus.Subscribe<Ping>(p => pings.Add(p.Value));
        using var b = _bus.Subscribe<Pong>(_ => pongs++);

        _bus.Publish(new Ping(1));
        _bus.Publish(new Ping(2));

        Assert.Equal([1, 2], pings);
        Assert.Equal(0, pongs);
    }

    [Fact]
    public void Disposing_subscription_stops_delivery()
    {
        var count = 0;
        var subscription = _bus.Subscribe<Ping>(_ => count++);

        _bus.Publish(new Ping(1));
        subscription.Dispose();
        subscription.Dispose();
        _bus.Publish(new Ping(2));

        Assert.Equal(1, count);
    }

    [Fact]
    public void Failing_handler_does_not_block_other_handlers()
    {
        var delivered = false;
        using var a = _bus.Subscribe<Ping>(_ => throw new InvalidOperationException("defeito"));
        using var b = _bus.Subscribe<Ping>(_ => delivered = true);

        _bus.Publish(new Ping(1));

        Assert.True(delivered);
    }

    [Fact]
    public void Handler_may_unsubscribe_itself_during_publish()
    {
        var count = 0;
        IDisposable? subscription = null;
        subscription = _bus.Subscribe<Ping>(_ =>
        {
            count++;
            subscription!.Dispose();
        });

        _bus.Publish(new Ping(1));
        _bus.Publish(new Ping(2));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Concurrent_publish_and_subscribe_is_safe()
    {
        var total = 0;
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                using var s = _bus.Subscribe<Ping>(_ => Interlocked.Increment(ref total));
                _bus.Publish(new Ping(i));
            }
        }));

        await Task.WhenAll(tasks);

        Assert.True(total >= 8 * 500);
    }
}
