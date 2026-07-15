using DisplayLight.Core.Power;

namespace DisplayLight.Core.Tests.Power;

public sealed class DisplayTimeoutCatalogTests
{
    [Fact]
    public void AllReturnsSliderPresetsInDisplayOrder()
    {
        DisplayTimeoutPreset[] expected =
        [
            DisplayTimeoutPreset.OneMinute,
            DisplayTimeoutPreset.FiveMinutes,
            DisplayTimeoutPreset.TenMinutes,
            DisplayTimeoutPreset.ThirtyMinutes,
            DisplayTimeoutPreset.SixtyMinutes,
            DisplayTimeoutPreset.Never,
        ];

        Assert.Equal(expected, DisplayTimeoutCatalog.All);
    }

    [Theory]
    [InlineData(DisplayTimeoutPreset.OneMinute, 60u)]
    [InlineData(DisplayTimeoutPreset.FiveMinutes, 300u)]
    [InlineData(DisplayTimeoutPreset.TenMinutes, 600u)]
    [InlineData(DisplayTimeoutPreset.ThirtyMinutes, 1800u)]
    [InlineData(DisplayTimeoutPreset.SixtyMinutes, 3600u)]
    [InlineData(DisplayTimeoutPreset.Never, 0u)]
    public void ToSecondsReturnsWindowsValue(DisplayTimeoutPreset preset, uint expected)
    {
        Assert.Equal(expected, DisplayTimeoutCatalog.ToSeconds(preset));
    }

    [Fact]
    public void TryFromSecondsRejectsCustomValue()
    {
        bool found = DisplayTimeoutCatalog.TryFromSeconds(15 * 60, out DisplayTimeoutPreset preset);

        Assert.False(found);
        Assert.Equal(DisplayTimeoutPreset.Unknown, preset);
    }

    [Theory]
    [InlineData(0u, DisplayTimeoutPreset.Never)]
    [InlineData(15u * 60u, DisplayTimeoutPreset.TenMinutes)]
    [InlineData(50u * 60u, DisplayTimeoutPreset.SixtyMinutes)]
    public void FindNearestReturnsClosestSliderPreset(uint seconds, DisplayTimeoutPreset expected)
    {
        Assert.Equal(expected, DisplayTimeoutCatalog.FindNearest(seconds));
    }
}
