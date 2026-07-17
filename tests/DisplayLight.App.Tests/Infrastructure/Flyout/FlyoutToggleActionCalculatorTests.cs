using DisplayLight.App.Infrastructure.Flyout;

namespace DisplayLight.App.Tests.Infrastructure.Flyout;

public sealed class FlyoutToggleActionCalculatorTests
{
    [Fact]
    public void CalculateKeepsClosingWhenActivationArrivesDuringClose()
    {
        Assert.Equal(FlyoutToggleAction.Show, FlyoutToggleActionCalculator.Calculate(false, false));
        Assert.Equal(FlyoutToggleAction.Hide, FlyoutToggleActionCalculator.Calculate(true, false));
        Assert.Equal(FlyoutToggleAction.Ignore, FlyoutToggleActionCalculator.Calculate(true, true));
    }
}
