using DisplayLight.Core.Power;

namespace DisplayLight.Core.Tests.Power;

public sealed class DisplayOffCountdownTests
{
    [Fact]
    public void TickCompletesOnlyAfterThreeAdvances()
    {
        DisplayOffCountdown countdown = new();
        countdown.Start();

        Assert.Equal(3, countdown.RemainingSeconds);
        Assert.False(countdown.Tick());
        Assert.False(countdown.Tick());
        Assert.True(countdown.Tick());
        Assert.False(countdown.IsActive);
        Assert.False(countdown.Tick());
    }

    [Fact]
    public void CancelPreventsCompletion()
    {
        DisplayOffCountdown countdown = new();
        countdown.Start();

        countdown.Cancel();

        Assert.False(countdown.IsActive);
        Assert.False(countdown.Tick());
    }
}
