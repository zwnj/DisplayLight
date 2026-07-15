using DisplayLight.Core.Power;

namespace DisplayLight.Core.Settings;

/// <summary>
/// Contains the user preferences that are safe to restore at application startup.
/// </summary>
public sealed record UserSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DisplayTimeoutPreset SelectedAcTimeout { get; init; } = DisplayTimeoutPreset.TenMinutes;

    public DisplayTimeoutPreset SelectedBatteryTimeout { get; init; } = DisplayTimeoutPreset.TenMinutes;

    public bool IsAcPowerOnly { get; init; }

    /// <summary>
    /// Replaces unsupported persisted values with safe defaults.
    /// </summary>
    public UserSettings Normalize() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        SelectedAcTimeout = NormalizePreset(SelectedAcTimeout),
        SelectedBatteryTimeout = NormalizePreset(SelectedBatteryTimeout),
    };

    private static DisplayTimeoutPreset NormalizePreset(DisplayTimeoutPreset preset) =>
        DisplayTimeoutCatalog.All.Contains(preset)
            ? preset
            : DisplayTimeoutPreset.TenMinutes;
}
