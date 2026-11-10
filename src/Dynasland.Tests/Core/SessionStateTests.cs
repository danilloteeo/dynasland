using Dynasland.Core.Session;

namespace Dynasland.Tests.Core;

public class SessionStateTests
{
    [Fact]
    public void Lock_and_unlock_toggle_interruption()
    {
        var state = new SessionState();

        Assert.True(state.Apply(SessionChange.Locked));
        Assert.True(state.IsInterrupted);
        Assert.False(state.Apply(SessionChange.Locked));

        Assert.True(state.Apply(SessionChange.Unlocked));
        Assert.False(state.IsInterrupted);
    }

    [Fact]
    public void Resume_with_lock_screen_stays_interrupted_until_unlock()
    {
        var state = new SessionState();

        state.Apply(SessionChange.Locked);
        state.Apply(SessionChange.Suspending);

        Assert.False(state.Apply(SessionChange.Resumed));
        Assert.True(state.IsInterrupted);

        Assert.True(state.Apply(SessionChange.Unlocked));
        Assert.False(state.IsInterrupted);
    }

    [Fact]
    public void Suspend_without_lock_is_restored_on_resume()
    {
        var state = new SessionState();

        Assert.True(state.Apply(SessionChange.Suspending));
        Assert.True(state.IsSuspended);
        Assert.True(state.Apply(SessionChange.Resumed));
        Assert.False(state.IsInterrupted);
    }

    [Fact]
    public void Unlock_without_lock_does_nothing()
    {
        var state = new SessionState();

        Assert.False(state.Apply(SessionChange.Unlocked));
        Assert.False(state.Apply(SessionChange.Resumed));
        Assert.False(state.IsInterrupted);
    }
}
