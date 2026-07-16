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
            FlyoutMotionCalculator.OpeningDistanceDips,
            1.5);

        Assert.Equal(new NativePoint(expectedX, expectedY), result);
    }

    [Fact]
    public void InterpolationUsesEaseOutAndFinishesAtTarget()
    {
        NativePoint halfway = FlyoutMotionCalculator.Interpolate(
            new NativePoint(0, 116),
            new NativePoint(0, 100),
            0.5);
        NativePoint finished = FlyoutMotionCalculator.Interpolate(
            new NativePoint(0, 116),
            new NativePoint(0, 100),
            1);

        Assert.Equal(new NativePoint(0, 104), halfway);
        Assert.Equal(new NativePoint(0, 100), finished);
    }

    [Fact]
    public void OpeningOpacityBecomesFullyVisibleBeforeMotionFinishes()
    {
        double early = FlyoutMotionCalculator.InterpolateOpacity(
            FlyoutMotionCalculator.OpeningStartOpacity,
            1,
            0.275);
        double halfway = FlyoutMotionCalculator.InterpolateOpacity(
            FlyoutMotionCalculator.OpeningStartOpacity,
            1,
            0.55);

        Assert.Equal(0.86, early, precision: 2);
        Assert.Equal(1, halfway);
    }
}
