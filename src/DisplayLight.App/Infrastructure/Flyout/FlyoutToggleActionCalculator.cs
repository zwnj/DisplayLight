namespace DisplayLight.App.Infrastructure.Flyout;

internal static class FlyoutToggleActionCalculator
{
    internal static FlyoutToggleAction Calculate(bool isVisible, bool isClosing)
    {
        if (!isVisible)
        {
            return FlyoutToggleAction.Show;
        }

        return isClosing
            ? FlyoutToggleAction.Ignore
            : FlyoutToggleAction.Hide;
    }
}

internal enum FlyoutToggleAction
{
    Show = 0,
    Hide = 1,
    Ignore = 2,
}
