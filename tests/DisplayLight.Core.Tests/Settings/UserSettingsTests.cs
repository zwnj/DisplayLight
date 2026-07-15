using DisplayLight.Core.Power;
using DisplayLight.Core.Settings;

namespace DisplayLight.Core.Tests.Settings;

public sealed class UserSettingsTests
{
    [Fact]
    public void NormalizeReplacesUnsupportedValues()
    {
        UserSettings settings = new()
        {
            SchemaVersion = 999,
            SelectedAcTimeout = DisplayTimeoutPreset.Unknown,
            SelectedBatteryTimeout = (DisplayTimeoutPreset)999,
            IsAcPowerOnly = true,
        };

        UserSettings normalized = settings.Normalize();

        Assert.Equal(UserSettings.CurrentSchemaVersion, normalized.SchemaVersion);
        Assert.Equal(DisplayTimeoutPreset.TenMinutes, normalized.SelectedAcTimeout);
        Assert.Equal(DisplayTimeoutPreset.TenMinutes, normalized.SelectedBatteryTimeout);
        Assert.True(normalized.IsAcPowerOnly);
    }
}
