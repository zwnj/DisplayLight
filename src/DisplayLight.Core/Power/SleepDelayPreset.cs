namespace DisplayLight.Core.Power;

/// <summary>
/// Identifies the supported delays between display power-down and system sleep.
/// </summary>
public enum SleepDelayPreset
{
    Unknown = 0,
    SameTime = 1,
    FiveMinutes = 2,
    FifteenMinutes = 3,
    ThirtyMinutes = 4,
    SixtyMinutes = 5,
    Never = 6,
}
