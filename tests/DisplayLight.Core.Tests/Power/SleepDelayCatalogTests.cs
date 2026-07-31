using DisplayLight.Core.Power;

namespace DisplayLight.Core.Tests.Power;

public sealed class SleepDelayCatalogTests
{
    [Theory]
    [InlineData(SleepDelayPreset.SameTime, 0u)]
    [InlineData(SleepDelayPreset.FiveMinutes, 300u)]
    [InlineData(SleepDelayPreset.FifteenMinutes, 900u)]
    [InlineData(SleepDelayPreset.ThirtyMinutes, 1800u)]
    [InlineData(SleepDelayPreset.SixtyMinutes, 3600u)]
    public void FinitePresetsRoundTrip(SleepDelayPreset preset, uint seconds)
    {
        Assert.Equal(seconds, SleepDelayCatalog.ToSeconds(preset));
        Assert.True(SleepDelayCatalog.TryFromSeconds(seconds, out SleepDelayPreset actual));
        Assert.Equal(preset, actual);
    }

    [Fact]
    public void NeverHasNoFiniteDelay()
    {
        Assert.Throws<ArgumentException>(() => SleepDelayCatalog.ToSeconds(SleepDelayPreset.Never));
    }
}
