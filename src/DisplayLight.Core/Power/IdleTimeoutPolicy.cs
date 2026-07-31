namespace DisplayLight.Core.Power;

/// <summary>
/// Converts between DisplayLight's relative timeout model and existing Windows values.
/// </summary>
public static class IdleTimeoutPolicy
{
    public static IdleTimeoutInterpretation Interpret(uint displaySeconds, uint sleepSeconds)
    {
        if (displaySeconds == 0)
        {
            return sleepSeconds == 0
                ? new(
                    IdleTimeoutInterpretationKind.ExactPreset,
                    displaySeconds,
                    sleepSeconds,
                    DelaySeconds: null,
                    DisplayTimeoutPreset.Never,
                    SleepDelayPreset.Never)
                : IndividuallyConfigured(displaySeconds, sleepSeconds);
        }

        if (sleepSeconds != 0 && sleepSeconds < displaySeconds)
        {
            return IndividuallyConfigured(displaySeconds, sleepSeconds);
        }

        uint? delaySeconds = sleepSeconds == 0
            ? null
            : sleepSeconds - displaySeconds;
        bool hasDisplayPreset = DisplayTimeoutCatalog.TryFromSeconds(
            displaySeconds,
            out DisplayTimeoutPreset displayPreset);
        bool hasDelayPreset;
        SleepDelayPreset sleepDelayPreset;

        if (sleepSeconds == 0)
        {
            hasDelayPreset = true;
            sleepDelayPreset = SleepDelayPreset.Never;
        }
        else
        {
            hasDelayPreset = SleepDelayCatalog.TryFromSeconds(
                delaySeconds!.Value,
                out sleepDelayPreset);
        }

        return new(
            hasDisplayPreset && hasDelayPreset
                ? IdleTimeoutInterpretationKind.ExactPreset
                : IdleTimeoutInterpretationKind.Custom,
            displaySeconds,
            sleepSeconds,
            delaySeconds,
            hasDisplayPreset ? displayPreset : DisplayTimeoutPreset.Unknown,
            hasDelayPreset ? sleepDelayPreset : SleepDelayPreset.Unknown);
    }

    private static IdleTimeoutInterpretation IndividuallyConfigured(
        uint displaySeconds,
        uint sleepSeconds) =>
        new(
            IdleTimeoutInterpretationKind.IndividuallyConfigured,
            displaySeconds,
            sleepSeconds,
            DelaySeconds: null,
            DisplayTimeoutPreset.Unknown,
            SleepDelayPreset.Unknown);
}
