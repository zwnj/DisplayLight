using DisplayLight.App.Infrastructure.Flyout;

namespace DisplayLight.App.Tests.Infrastructure.Flyout;

public sealed class FlyoutMotionCalculatorTests
{
    [Theory]
    [InlineData(TaskbarEdge.Bottom, 100, 224)]
    [InlineData(TaskbarEdge.Top, 100, 176)]
    [InlineData(TaskbarEdge.Left, 76, 200)]
    [InlineData(TaskbarEdge.Right, 124, 200)]
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

        Assert.Equal(new NativePoint(0, 102), halfway);
        Assert.Equal(new NativePoint(0, 100), finished);
    }
}
