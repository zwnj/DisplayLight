namespace DisplayLight.Core.Power;

/// <summary>
/// Contains the active scheme's display and system sleep timeouts in seconds.
/// </summary>
public sealed record IdleTimeoutValues(
    uint AcDisplaySeconds,
    uint AcSleepSeconds,
    uint BatteryDisplaySeconds,
    uint BatterySleepSeconds)
{
    public IdleTimeoutPair For(PowerSettingTarget target) => target switch
    {
        PowerSettingTarget.AcPower => new(AcDisplaySeconds, AcSleepSeconds),
        PowerSettingTarget.Battery => new(BatteryDisplaySeconds, BatterySleepSeconds),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported power setting target."),
    };
}

public sealed record IdleTimeoutPair(uint DisplaySeconds, uint SleepSeconds);
