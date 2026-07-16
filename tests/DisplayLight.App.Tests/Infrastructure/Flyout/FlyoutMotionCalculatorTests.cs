using DisplayLight.App.Infrastructure.Flyout;

namespace DisplayLight.App.Tests.Infrastructure.Flyout;

public sealed class FlyoutMotionCalculatorTests
{
    [Theory]
    [InlineData(TaskbarEdge.Bottom, 100, 248)]
    [InlineData(TaskbarEdge.Top, 100, 152)]
    [InlineData(TaskbarEdge.Left, 52, 200)]
    [InlineData(TaskbarEdge.Right, 148, 200)]
    internal void OffsetsOpeningPositionTowardsTaskbar(
        TaskbarEdge edge,
        int expectedX,
        int expectedY)
    {
        NativePoint result = FlyoutMotionCalculator.OffsetTowardsTaskbar(
            new NativePoint(100, 200),
            edge,
            48);

        Assert.Equal(new NativePoint(expectedX, expectedY), result);
    }

    [Theory]
    [InlineData(TaskbarEdge.Bottom, 508)]
    [InlineData(TaskbarEdge.Top, 508)]
    [InlineData(TaskbarEdge.Left, 380)]
    [InlineData(TaskbarEdge.Right, 380)]
    internal void HiddenDistanceCoversWindowExtentAndPlacementGap(
        TaskbarEdge edge,
        int expected)
    {
        int result = FlyoutMotionCalculator.CalculateHiddenDistance(
            new NativeSize(372, 500),
            edge);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void OpeningInterpolationUsesCubicEaseOutAndFinishesAtTarget()
    {
        NativePoint halfway = FlyoutMotionCalculator.InterpolateOpening(
            new NativePoint(0, 116),
            new NativePoint(0, 100),
            0.5);
        NativePoint finished = FlyoutMotionCalculator.InterpolateOpening(
            new NativePoint(0, 116),
            new NativePoint(0, 100),
            1);

        Assert.Equal(new NativePoint(0, 102), halfway);
        Assert.Equal(new NativePoint(0, 100), finished);
    }

    [Fact]
    public void ClosingInterpolationUsesCubicEaseInAndFinishesAtHiddenPosition()
    {
        NativePoint halfway = FlyoutMotionCalculator.InterpolateClosing(
            new NativePoint(0, 100),
            new NativePoint(0, 116),
            0.5);
        NativePoint finished = FlyoutMotionCalculator.InterpolateClosing(
            new NativePoint(0, 100),
            new NativePoint(0, 116),
            1);

        Assert.Equal(new NativePoint(0, 102), halfway);
        Assert.Equal(new NativePoint(0, 116), finished);
    }
}
