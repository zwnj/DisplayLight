namespace DisplayLight.Core.Power;

/// <summary>
/// Converts display timeout presets to the values used by Windows power settings.
/// </summary>
public static class DisplayTimeoutCatalog
{
    private static readonly DisplayTimeoutPreset[] OrderedPresets =
    [
        DisplayTimeoutPreset.OneMinute,
        DisplayTimeoutPreset.FiveMinutes,
        DisplayTimeoutPreset.TenMinutes,
        DisplayTimeoutPreset.ThirtyMinutes,
        DisplayTimeoutPreset.SixtyMinutes,
        DisplayTimeoutPreset.Never,
    ];

    /// <summary>
    /// Gets the presets in their slider order.
    /// </summary>
    public static IReadOnlyList<DisplayTimeoutPreset> All { get; } = Array.AsReadOnly(OrderedPresets);

    /// <summary>
    /// Converts a preset to seconds, where zero means that the display never turns off.
    /// </summary>
    public static uint ToSeconds(DisplayTimeoutPreset preset) => preset switch
    {
        DisplayTimeoutPreset.OneMinute => 60,
        DisplayTimeoutPreset.FiveMinutes => 5 * 60,
        DisplayTimeoutPreset.TenMinutes => 10 * 60,
        DisplayTimeoutPreset.ThirtyMinutes => 30 * 60,
        DisplayTimeoutPreset.SixtyMinutes => 60 * 60,
        DisplayTimeoutPreset.Never => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported display timeout preset."),
    };

    /// <summary>
    /// Finds the exact preset for a Windows timeout value.
    /// </summary>
    public static bool TryFromSeconds(uint seconds, out DisplayTimeoutPreset preset)
    {
        foreach (DisplayTimeoutPreset candidate in OrderedPresets)
        {
            if (ToSeconds(candidate) == seconds)
            {
                preset = candidate;
                return true;
            }
        }

        preset = DisplayTimeoutPreset.Unknown;
        return false;
    }

    /// <summary>
    /// Finds the nearest slider preset without treating a custom value as an exact match.
    /// </summary>
    public static DisplayTimeoutPreset FindNearest(uint seconds)
    {
        if (seconds == 0)
        {
            return DisplayTimeoutPreset.Never;
        }

        return OrderedPresets
            .Where(preset => preset != DisplayTimeoutPreset.Never)
            .MinBy(preset => Math.Abs((long)ToSeconds(preset) - seconds));
    }
}
