namespace DisplayLight.Core.Power;

/// <summary>
/// Evaluates the manual request and the optional AC-only guard independently of Windows APIs.
/// </summary>
public static class SleepPreventionPolicy
{
    /// <summary>
    /// Determines whether the process should own a system-sleep prevention request.
    /// </summary>
    public static SleepPreventionDecision Evaluate(
        bool isRequested,
        bool isAcPowerOnly,
        PowerSource powerSource)
    {
        if (!isRequested)
        {
            return new(false, SleepPreventionReason.NotRequested);
        }

        if (!isAcPowerOnly)
        {
            return new(true, SleepPreventionReason.Active);
        }

        return powerSource switch
        {
            PowerSource.AcPower => new(true, SleepPreventionReason.Active),
            PowerSource.Battery => new(false, SleepPreventionReason.WaitingForAcPower),
            _ => new(false, SleepPreventionReason.PowerSourceUnavailable),
        };
    }
}
