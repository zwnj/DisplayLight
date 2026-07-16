using DisplayLight.App.Infrastructure.Flyout;

namespace DisplayLight.App.Tests.Infrastructure.Flyout;

public sealed class FlyoutPlacementCalculatorTests
{
    [Fact]
    public void PlacesFlyoutAboveBottomTaskbarAndRightAlignsIt()
    {
        NativePoint result = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(1880, 1040, 1920, 1080),
            new NativeRectangle(0, 0, 1920, 1040),
            new NativeSize(392, 600));

        Assert.Equal(new NativePoint(1516, 428), result);
    }

    [Fact]
    public void PlacesFlyoutBelowTopTaskbar()
    {
        NativePoint result = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(1200, 0, 1240, 40),
            new NativeRectangle(0, 40, 1920, 1080),
            new NativeSize(392, 600));

        Assert.Equal(new NativePoint(848, 52), result);
    }

    [Fact]
    public void PlacesFlyoutBesideVerticalTaskbars()
    {
        NativePoint leftResult = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(0, 900, 40, 940),
            new NativeRectangle(40, 0, 1920, 1080),
            new NativeSize(392, 600));
        NativePoint rightResult = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(1880, 900, 1920, 940),
            new NativeRectangle(0, 0, 1880, 1080),
            new NativeSize(392, 600));

        Assert.Equal(new NativePoint(52, 340), leftResult);
        Assert.Equal(new NativePoint(1476, 340), rightResult);
    }

    [Fact]
    public void ClampsOversizedCoordinatesInsideWorkAreaMargin()
    {
        NativePoint result = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(-1910, 1040, -1880, 1080),
            new NativeRectangle(-1920, 0, 0, 1040),
            new NativeSize(600, 1000));

        Assert.InRange(result.X, -1908, -612);
        Assert.InRange(result.Y, 12, 28);
    }
}
