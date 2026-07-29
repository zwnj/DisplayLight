using DisplayLight.App.Infrastructure.Tray;

namespace DisplayLight.App.Tests.Infrastructure.Tray;

public sealed class TrayIconStateCalculatorTests
{
    [Theory]
    [InlineData(false, false, false, 0)]
    [InlineData(true, true, false, 1)]
    [InlineData(true, false, false, 2)]
    [InlineData(false, false, true, 3)]
    [InlineData(true, true, true, 3)]
    public void CalculateReturnsExpectedState(
        bool isRequested,
        bool isActive,
        bool hasError,
        int expectedValue)
    {
        TrayIconState expected = (TrayIconState)expectedValue;
        TrayIconState actual = TrayIconStateCalculator.Calculate(
            isRequested,
            isActive,
            hasError);

        Assert.Equal(expected, actual);
    }
}
