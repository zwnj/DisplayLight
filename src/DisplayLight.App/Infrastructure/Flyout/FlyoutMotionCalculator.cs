namespace DisplayLight.App.Infrastructure.Flyout;

internal static class FlyoutMotionCalculator
{
    internal const int OpeningDurationMilliseconds = 250;
    internal const int ClosingDurationMilliseconds = 170;
    internal const int ContentRevealDurationMilliseconds = 120;

    internal static NativePoint OffsetTowardsTaskbar(
        NativePoint location,
        TaskbarEdge edge,
        int distancePixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distancePixels);

        return edge switch
        {
            TaskbarEdge.Bottom => location with { Y = location.Y + distancePixels },
            TaskbarEdge.Top => location with { Y = location.Y - distancePixels },
            TaskbarEdge.Left => location with { X = location.X - distancePixels },
            TaskbarEdge.Right => location with { X = location.X + distancePixels },
            _ => location,
        };
    }

    internal static int CalculateHiddenDistance(
        NativeSize size,
        TaskbarEdge edge,
        int placementGap = FlyoutPlacementCalculator.DefaultGap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height);
        ArgumentOutOfRangeException.ThrowIfNegative(placementGap);

        int windowExtent = edge is TaskbarEdge.Top or TaskbarEdge.Bottom
            ? size.Height
            : size.Width;
        return checked(windowExtent + placementGap);
    }

    internal static NativePoint InterpolateOpening(
        NativePoint start,
        NativePoint end,
        double progress)
    {
        double normalized = Math.Clamp(progress, 0, 1);
        double eased = 1 - Math.Pow(1 - normalized, 3);
        return Interpolate(start, end, eased);
    }

    internal static NativePoint InterpolateClosing(
        NativePoint start,
        NativePoint end,
        double progress)
    {
        double normalized = Math.Clamp(progress, 0, 1);
        double eased = Math.Pow(normalized, 3);
        return Interpolate(start, end, eased);
    }

    internal static double InterpolateContentOpacity(double startOpacity, double progress)
    {
        double normalized = Math.Clamp(progress, 0, 1);
        double smoothStep = normalized * normalized * (3 - (2 * normalized));
        return startOpacity + ((1 - startOpacity) * smoothStep);
    }

    private static NativePoint Interpolate(NativePoint start, NativePoint end, double easedProgress) =>
        new(
            (int)Math.Round(start.X + ((end.X - start.X) * easedProgress)),
            (int)Math.Round(start.Y + ((end.Y - start.Y) * easedProgress)));
}
