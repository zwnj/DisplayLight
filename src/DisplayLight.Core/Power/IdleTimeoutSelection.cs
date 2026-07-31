namespace DisplayLight.Core.Power;

/// <summary>
/// Contains one display timeout and the delay from that timeout to system sleep.
/// </summary>
public sealed record IdleTimeoutSelection(
    DisplayTimeoutPreset DisplayTimeout,
    SleepDelayPreset SleepDelay)
{
    /// <summary>
    /// Converts the relative selection to the absolute system idle timeout used by Windows.
    /// </summary>
    public uint ToSleepTimeoutSeconds()
    {
        if (!DisplayTimeoutCatalog.All.Contains(DisplayTimeout))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DisplayTimeout),
                DisplayTimeout,
                "Unsupported display timeout preset.");
        }

        if (!SleepDelayCatalog.All.Contains(SleepDelay))
        {
            throw new ArgumentOutOfRangeException(
                nameof(SleepDelay),
                SleepDelay,
                "Unsupported sleep delay preset.");
        }

        if (DisplayTimeout == DisplayTimeoutPreset.Never || SleepDelay == SleepDelayPreset.Never)
        {
            return 0;
        }

        uint displaySeconds = DisplayTimeoutCatalog.ToSeconds(DisplayTimeout);
        uint delaySeconds = SleepDelayCatalog.ToSeconds(SleepDelay);
        return checked(displaySeconds + delaySeconds);
    }
}
