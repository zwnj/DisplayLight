namespace DisplayLight.Core.Power;

/// <summary>
/// Explains why the physical system-sleep prevention request should be held or released.
/// </summary>
public sealed record SleepPreventionDecision(bool ShouldPreventSleep, SleepPreventionReason Reason);

/// <summary>
/// Identifies the rule that produced a sleep-prevention decision.
/// </summary>
public enum SleepPreventionReason
{
    NotRequested = 0,
    Active = 1,
    WaitingForAcPower = 2,
    PowerSourceUnavailable = 3,
}
