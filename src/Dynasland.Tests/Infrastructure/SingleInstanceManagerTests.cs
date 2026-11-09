using Dynasland.Infrastructure.Services;

namespace Dynasland.Tests.Infrastructure;

public class SingleInstanceManagerTests
{
    private static string UniqueId() => "DynaslandTest." + Guid.NewGuid().ToString("N");

    [Fact]
    public void Second_instance_is_rejected_while_first_is_alive()
    {
        var id = UniqueId();
        using var first = new SingleInstanceManager(id);
        using var second = new SingleInstanceManager(id);

        Assert.True(first.TryAcquire());
        Assert.False(RunOnOtherThread(second.TryAcquire));
    }

    [Fact]
    public void Instance_can_be_acquired_again_after_dispose()
    {
        var id = UniqueId();
        using (var first = new SingleInstanceManager(id))
        {
            Assert.True(first.TryAcquire());
        }

        using var again = new SingleInstanceManager(id);
        Assert.True(again.TryAcquire());
    }

    [Fact]
    public void Signal_from_second_instance_reaches_first()
    {
        var id = UniqueId();
        using var first = new SingleInstanceManager(id);
        using var activated = new ManualResetEventSlim();
        first.ActivationRequested += (_, _) => activated.Set();
        Assert.True(first.TryAcquire());

        using var second = new SingleInstanceManager(id);
        Assert.True(second.SignalFirstInstance());

        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    // Mutex é reentrante na mesma thread; a segunda instância precisa de outra thread para simular outro processo.
    private static bool RunOnOtherThread(Func<bool> action)
    {
        var result = false;
        var thread = new Thread(() => result = action());
        thread.Start();
        thread.Join();
        return result;
    }
}
