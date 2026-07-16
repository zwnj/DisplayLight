using DisplayLight.App.Infrastructure.Flyout;

namespace DisplayLight.App.Tests.Infrastructure.Flyout;

public sealed class FlyoutPlacementCalculatorTests
{
    [Fact]
    public void PlacesFlyoutAboveBottomTaskbarAndRightAlignsIt()
    {
        FlyoutPlacement result = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(1880, 1040, 1920, 1080),
            new NativeRectangle(0, 0, 1920, 1040),
            new NativeSize(392, 600));

        Assert.Equal(new NativePoint(1516, 428), result.Location);
        Assert.Equal(TaskbarEdge.Bottom, result.Edge);
    }

    [Fact]
    public void CentersFlyoutOnBottomTaskbarIconWhenWorkAreaAllowsIt()
    {
        FlyoutPlacement result = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(940, 1040, 980, 1080),
            new NativeRectangle(0, 0, 1920, 1040),
            new NativeSize(392, 600));

        Assert.Equal(new NativePoint(764, 428), result.Location);
        Assert.Equal(960, result.Location.X + 196);
    }

    [Fact]
    public void PlacesFlyoutBelowTopTaskbar()
    {
        FlyoutPlacement result = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(1200, 0, 1240, 40),
            new NativeRectangle(0, 40, 1920, 1080),
            new NativeSize(392, 600));

        Assert.Equal(new NativePoint(1024, 52), result.Location);
        Assert.Equal(TaskbarEdge.Top, result.Edge);
    }

    [Fact]
    public void PlacesFlyoutBesideVerticalTaskbars()
    {
        FlyoutPlacement leftResult = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(0, 900, 40, 940),
            new NativeRectangle(40, 0, 1920, 1080),
            new NativeSize(392, 600));
        FlyoutPlacement rightResult = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(1880, 900, 1920, 940),
            new NativeRectangle(0, 0, 1880, 1080),
            new NativeSize(392, 600));

        Assert.Equal(new NativePoint(52, 468), leftResult.Location);
        Assert.Equal(TaskbarEdge.Left, leftResult.Edge);
        Assert.Equal(new NativePoint(1476, 468), rightResult.Location);
        Assert.Equal(TaskbarEdge.Right, rightResult.Edge);
    }

    [Fact]
    public void ClampsOversizedCoordinatesInsideWorkAreaMargin()
    {
        FlyoutPlacement result = FlyoutPlacementCalculator.Calculate(
            new NativeRectangle(-1910, 1040, -1880, 1080),
            new NativeRectangle(-1920, 0, 0, 1040),
            new NativeSize(600, 1000));

        Assert.InRange(result.Location.X, -1908, -612);
        Assert.InRange(result.Location.Y, 12, 28);
    }
}
