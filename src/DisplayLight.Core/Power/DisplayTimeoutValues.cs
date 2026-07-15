namespace DisplayLight.Core.Power;

/// <summary>
/// Contains the current AC and battery display timeout values in seconds.
/// </summary>
public sealed record DisplayTimeoutValues(uint AcSeconds, uint BatterySeconds);
