using DisplayLight.Core.Power;

namespace DisplayLight.Core.Settings;

/// <summary>
/// Contains the user preferences that are safe to restore at application startup.
/// </summary>
public sealed record UserSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DisplayTimeoutPreset SelectedAcTimeout { get; init; } = DisplayTimeoutPreset.TenMinutes;

    public DisplayTimeoutPreset SelectedBatteryTimeout { get; init; } = DisplayTimeoutPreset.TenMinutes;

    public SleepDelayPreset SelectedAcSleepDelay { get; init; } = SleepDelayPreset.FifteenMinutes;

    public SleepDelayPreset SelectedBatterySleepDelay { get; init; } = SleepDelayPreset.FifteenMinutes;

    public bool IsAcPowerOnly { get; init; }

    /// <summary>
    /// Replaces unsupported persisted values with safe defaults.
    /// </summary>
    public UserSettings Normalize() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        SelectedAcTimeout = NormalizePreset(SelectedAcTimeout),
        SelectedBatteryTimeout = NormalizePreset(SelectedBatteryTimeout),
        SelectedAcSleepDelay = NormalizeSleepDelay(SelectedAcSleepDelay),
        SelectedBatterySleepDelay = NormalizeSleepDelay(SelectedBatterySleepDelay),
    };

    private static DisplayTimeoutPreset NormalizePreset(DisplayTimeoutPreset preset) =>
        DisplayTimeoutCatalog.All.Contains(preset)
            ? preset
            : DisplayTimeoutPreset.TenMinutes;

    private static SleepDelayPreset NormalizeSleepDelay(SleepDelayPreset preset) =>
        SleepDelayCatalog.All.Contains(preset)
            ? preset
            : SleepDelayPreset.FifteenMinutes;
}
