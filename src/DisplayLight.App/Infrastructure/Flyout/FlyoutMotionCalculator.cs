namespace DisplayLight.App.Infrastructure.Flyout;

internal static class FlyoutMotionCalculator
{
    internal const double OpeningDistanceDips = 32;
    internal const double ClosingDistanceDips = 16;
    internal const double OpeningStartOpacity = 0.72;
    internal const int OpeningDurationMilliseconds = 220;
    internal const int ClosingDurationMilliseconds = 140;

    internal static NativePoint OffsetTowardsTaskbar(
        NativePoint location,
        TaskbarEdge edge,
        double distanceDips,
        double dpiScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distanceDips);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpiScale);

        int offset = (int)Math.Round(distanceDips * dpiScale, MidpointRounding.AwayFromZero);
        return edge switch
        {
            TaskbarEdge.Bottom => location with { Y = location.Y + offset },
            TaskbarEdge.Top => location with { Y = location.Y - offset },
            TaskbarEdge.Left => location with { X = location.X - offset },
            TaskbarEdge.Right => location with { X = location.X + offset },
            _ => location,
        };
    }

    internal static NativePoint Interpolate(NativePoint start, NativePoint end, double progress)
    {
        double normalized = Math.Clamp(progress, 0, 1);
        double eased = 1 - Math.Pow(1 - normalized, 2);
        return new NativePoint(
            (int)Math.Round(start.X + ((end.X - start.X) * eased)),
            (int)Math.Round(start.Y + ((end.Y - start.Y) * eased)));
    }

    internal static double InterpolateOpacity(double start, double end, double progress)
    {
        double normalized = Math.Clamp(progress, 0, 1);
        double opacityProgress = end > start
            ? Math.Min(1, normalized / 0.55)
            : normalized;
        return start + ((end - start) * opacityProgress);
    }
}
