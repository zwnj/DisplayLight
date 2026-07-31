namespace DisplayLight.Core.Power;

/// <summary>
/// Converts system sleep delay presets to seconds.
/// </summary>
public static class SleepDelayCatalog
{
    private static readonly SleepDelayPreset[] OrderedPresets =
    [
        SleepDelayPreset.SameTime,
        SleepDelayPreset.FiveMinutes,
        SleepDelayPreset.FifteenMinutes,
        SleepDelayPreset.ThirtyMinutes,
        SleepDelayPreset.SixtyMinutes,
        SleepDelayPreset.Never,
    ];

    public static IReadOnlyList<SleepDelayPreset> All { get; } = Array.AsReadOnly(OrderedPresets);

    /// <summary>
    /// Converts a finite delay to seconds. Never has no finite representation.
    /// </summary>
    public static uint ToSeconds(SleepDelayPreset preset) => preset switch
    {
        SleepDelayPreset.SameTime => 0,
        SleepDelayPreset.FiveMinutes => 5 * 60,
        SleepDelayPreset.FifteenMinutes => 15 * 60,
        SleepDelayPreset.ThirtyMinutes => 30 * 60,
        SleepDelayPreset.SixtyMinutes => 60 * 60,
        SleepDelayPreset.Never => throw new ArgumentException(
            "The Never preset does not have a finite delay.",
            nameof(preset)),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported sleep delay preset."),
    };

    public static bool TryFromSeconds(uint seconds, out SleepDelayPreset preset)
    {
        foreach (SleepDelayPreset candidate in OrderedPresets)
        {
            if (candidate != SleepDelayPreset.Never && ToSeconds(candidate) == seconds)
            {
                preset = candidate;
                return true;
            }
        }

        preset = SleepDelayPreset.Unknown;
        return false;
    }
}
