namespace DisplayLight.App.Infrastructure.Tray;

internal enum TrayIconState
{
    Normal,
    SleepPreventionActive,
    WaitingForAcPower,
    Error,
}

internal static class TrayIconStateCalculator
{
    internal static TrayIconState Calculate(
        bool isSleepPreventionRequested,
        bool isSleepPreventionActive,
        bool hasError)
    {
        if (hasError)
        {
            return TrayIconState.Error;
        }

        if (isSleepPreventionActive)
        {
            return TrayIconState.SleepPreventionActive;
        }

        return isSleepPreventionRequested
            ? TrayIconState.WaitingForAcPower
            : TrayIconState.Normal;
    }
}
