namespace DisplayLight.Core.Power;

/// <summary>
/// Identifies the display timeout choices supported by the MVP.
/// </summary>
public enum DisplayTimeoutPreset
{
    Unknown = 0,
    OneMinute = 1,
    FiveMinutes = 2,
    TenMinutes = 3,
    ThirtyMinutes = 4,
    SixtyMinutes = 5,
    Never = 6,
}
