namespace DisplayLight.App.Infrastructure.Flyout;

internal static class FlyoutMotionCalculator
{
    internal const int OpeningDurationMilliseconds = 250;
    internal const int ClosingDurationMilliseconds = 170;
    internal const int ContentRevealDurationMilliseconds = 120;
    internal const int BoundsResizeDurationMilliseconds = 180;
    internal const double OpeningContentOpacity = 0.72;

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
        double smoothStep = SmoothStep(normalized);
        return startOpacity + ((1 - startOpacity) * smoothStep);
    }

    internal static NativePoint InterpolateBoundsLocation(
        NativePoint start,
        NativePoint end,
        double progress) =>
        Interpolate(start, end, SmoothStep(Math.Clamp(progress, 0, 1)));

    internal static NativeSize InterpolateBoundsSize(
        NativeSize start,
        NativeSize end,
        double progress)
    {
        double smoothStep = SmoothStep(Math.Clamp(progress, 0, 1));
        return new NativeSize(
            Math.Max(1, (int)Math.Round(start.Width + ((end.Width - start.Width) * smoothStep))),
            Math.Max(1, (int)Math.Round(start.Height + ((end.Height - start.Height) * smoothStep))));
    }

    internal static double CalculateDesiredSurfaceHeight(
        double contentHeight,
        double verticalMargin,
        double verticalBorderThickness)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(contentHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(verticalMargin);
        ArgumentOutOfRangeException.ThrowIfNegative(verticalBorderThickness);

        return contentHeight + verticalMargin + verticalBorderThickness;
    }

    private static NativePoint Interpolate(NativePoint start, NativePoint end, double easedProgress) =>
        new(
            (int)Math.Round(start.X + ((end.X - start.X) * easedProgress)),
            (int)Math.Round(start.Y + ((end.Y - start.Y) * easedProgress)));

    private static double SmoothStep(double normalized) =>
        normalized * normalized * (3 - (2 * normalized));
}
