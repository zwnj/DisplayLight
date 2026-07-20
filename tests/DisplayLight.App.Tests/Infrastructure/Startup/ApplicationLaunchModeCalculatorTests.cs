using DisplayLight.App.Infrastructure.Startup;

namespace DisplayLight.App.Tests.Infrastructure.Startup;

public sealed class ApplicationLaunchModeCalculatorTests
{
    [Theory]
    [InlineData("--startup")]
    [InlineData("--STARTUP")]
    public void RecognizesStartupArgumentWithoutCaseSensitivity(string argument)
    {
        Assert.True(ApplicationLaunchModeCalculator.IsStartupLaunch([argument]));
    }

    [Fact]
    public void TreatsNormalLaunchAsInteractive()
    {
        Assert.False(ApplicationLaunchModeCalculator.IsStartupLaunch([]));
        Assert.False(ApplicationLaunchModeCalculator.IsStartupLaunch(["--other"]));
    }
}
