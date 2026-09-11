using SlowRoad.Models;

namespace SlowRoad.Tests;

public class ErrorViewModelTests
{
    [Fact] // true when RequestId has a value
    public void ShowRequestId_True_WhenSet()
    {
        var model = new ErrorViewModel { RequestId = "abc-123" };
        Assert.True(model.ShowRequestId);
    }

    [Fact] // false when RequestId is empty
    public void ShowRequestId_False_WhenNull()
    {
        var model = new ErrorViewModel { RequestId = null };
        Assert.False(model.ShowRequestId);
    }
}
