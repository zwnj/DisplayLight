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

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1, 1)]
    internal void ContentOpacityUsesSmoothStepAfterSurfaceMotion(
        double progress,
        double expected)
    {
        double result = FlyoutMotionCalculator.InterpolateContentOpacity(0, progress);

        Assert.Equal(expected, result, precision: 3);
    }

    [Fact]
    public void OpeningKeepsContentVisibleBehindMovingSurface()
    {
        Assert.InRange(FlyoutMotionCalculator.OpeningContentOpacity, 0.5, 0.9);
        Assert.True(FlyoutMotionCalculator.OpeningContentOpacity < 1);
    }

    [Fact]
    public void BoundsInterpolationMovesAndResizesTogether()
    {
        NativePoint location = FlyoutMotionCalculator.InterpolateBoundsLocation(
            new NativePoint(100, 300),
            new NativePoint(100, 100),
            0.5);
        NativeSize size = FlyoutMotionCalculator.InterpolateBoundsSize(
            new NativeSize(372, 300),
            new NativeSize(372, 500),
            0.5);

        Assert.Equal(new NativePoint(100, 200), location);
        Assert.Equal(new NativeSize(372, 400), size);
    }

    [Fact]
    public void DesiredSurfaceHeightCanShrinkWithInnerContent()
    {
        double expanded = FlyoutMotionCalculator.CalculateDesiredSurfaceHeight(600, 32, 2);
        double collapsed = FlyoutMotionCalculator.CalculateDesiredSurfaceHeight(420, 32, 2);

        Assert.Equal(634, expanded);
        Assert.Equal(454, collapsed);
        Assert.True(collapsed < expanded);
    }
}
