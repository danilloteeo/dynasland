using Dynasland.App.Controls;

namespace Dynasland.Tests.Core;

public class RowCascadeTests
{
    [Fact]
    public void DelayFor_FirstRow_IsZero() => Assert.Equal(TimeSpan.Zero, RowCascade.DelayFor(0));

    [Fact]
    public void DelayFor_ThirdRow_Is90Ms() => Assert.Equal(TimeSpan.FromMilliseconds(90), RowCascade.DelayFor(3));

    [Fact]
    public void DelayFor_FarRow_IsCappedAt300Ms() => Assert.Equal(TimeSpan.FromMilliseconds(300), RowCascade.DelayFor(50));
}
