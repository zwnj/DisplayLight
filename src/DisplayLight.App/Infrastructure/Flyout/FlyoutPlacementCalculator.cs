namespace DisplayLight.App.Infrastructure.Flyout;

internal static class FlyoutPlacementCalculator
{
    internal const int DefaultGap = 8;
    internal const int DefaultWorkAreaMargin = 12;

    internal static NativePoint Calculate(
        NativeRectangle iconBounds,
        NativeRectangle workArea,
        NativeSize flyoutSize,
        int gap = DefaultGap,
        int margin = DefaultWorkAreaMargin)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(flyoutSize.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(flyoutSize.Height);

        int centerX = iconBounds.Left + (iconBounds.Width / 2);
        int centerY = iconBounds.Top + (iconBounds.Height / 2);
        int distanceToLeft = Math.Abs(centerX - workArea.Left);
        int distanceToRight = Math.Abs(workArea.Right - centerX);
        int distanceToTop = Math.Abs(centerY - workArea.Top);
        int distanceToBottom = Math.Abs(workArea.Bottom - centerY);
        int nearestEdge = Math.Min(Math.Min(distanceToLeft, distanceToRight), Math.Min(distanceToTop, distanceToBottom));

        int x;
        int y;
        if (nearestEdge == distanceToBottom)
        {
            x = iconBounds.Right - flyoutSize.Width;
            y = iconBounds.Top - flyoutSize.Height - gap;
        }
        else if (nearestEdge == distanceToTop)
        {
            x = iconBounds.Right - flyoutSize.Width;
            y = iconBounds.Bottom + gap;
        }
        else if (nearestEdge == distanceToLeft)
        {
            x = iconBounds.Right + gap;
            y = iconBounds.Bottom - flyoutSize.Height;
        }
        else
        {
            x = iconBounds.Left - flyoutSize.Width - gap;
            y = iconBounds.Bottom - flyoutSize.Height;
        }

        int maximumX = Math.Max(workArea.Left + margin, workArea.Right - margin - flyoutSize.Width);
        int maximumY = Math.Max(workArea.Top + margin, workArea.Bottom - margin - flyoutSize.Height);
        return new NativePoint(
            Math.Clamp(x, workArea.Left + margin, maximumX),
            Math.Clamp(y, workArea.Top + margin, maximumY));
    }
}

internal readonly record struct NativePoint(int X, int Y);

internal readonly record struct NativeSize(int Width, int Height);

internal readonly record struct NativeRectangle(int Left, int Top, int Right, int Bottom)
{
    internal int Width => Right - Left;

    internal int Height => Bottom - Top;
}
