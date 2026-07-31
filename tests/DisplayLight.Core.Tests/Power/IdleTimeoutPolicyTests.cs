using DisplayLight.Core.Power;

namespace DisplayLight.Core.Tests.Power;

public sealed class IdleTimeoutPolicyTests
{
    [Fact]
    public void SelectionAddsDelayToDisplayTimeout()
    {
        IdleTimeoutSelection selection = new(
            DisplayTimeoutPreset.TenMinutes,
            SleepDelayPreset.FifteenMinutes);

        Assert.Equal(1500u, selection.ToSleepTimeoutSeconds());
    }

    [Theory]
    [InlineData(DisplayTimeoutPreset.Never, SleepDelayPreset.FifteenMinutes)]
    [InlineData(DisplayTimeoutPreset.TenMinutes, SleepDelayPreset.Never)]
    public void NeverOnEitherSideDisablesSystemSleep(
        DisplayTimeoutPreset displayPreset,
        SleepDelayPreset sleepDelayPreset)
    {
        IdleTimeoutSelection selection = new(displayPreset, sleepDelayPreset);

        Assert.Equal(0u, selection.ToSleepTimeoutSeconds());
    }

    [Theory]
    [InlineData(DisplayTimeoutPreset.Unknown, SleepDelayPreset.Never)]
    [InlineData(DisplayTimeoutPreset.Never, SleepDelayPreset.Unknown)]
    public void SelectionRejectsUnsupportedPresetsEvenWhenOtherSideIsNever(
        DisplayTimeoutPreset displayPreset,
        SleepDelayPreset sleepDelayPreset)
    {
        IdleTimeoutSelection selection = new(displayPreset, sleepDelayPreset);

        Assert.Throws<ArgumentOutOfRangeException>(() => selection.ToSleepTimeoutSeconds());
    }

    [Theory]
    [InlineData(600u, 1500u, DisplayTimeoutPreset.TenMinutes, SleepDelayPreset.FifteenMinutes)]
    [InlineData(1800u, 1800u, DisplayTimeoutPreset.ThirtyMinutes, SleepDelayPreset.SameTime)]
    [InlineData(300u, 0u, DisplayTimeoutPreset.FiveMinutes, SleepDelayPreset.Never)]
    [InlineData(0u, 0u, DisplayTimeoutPreset.Never, SleepDelayPreset.Never)]
    public void InterpretRecognizesExactPresetPairs(
        uint displaySeconds,
        uint sleepSeconds,
        DisplayTimeoutPreset displayPreset,
        SleepDelayPreset delayPreset)
    {
        IdleTimeoutInterpretation result = IdleTimeoutPolicy.Interpret(displaySeconds, sleepSeconds);

        Assert.Equal(IdleTimeoutInterpretationKind.ExactPreset, result.Kind);
        Assert.Equal(displayPreset, result.DisplayPreset);
        Assert.Equal(delayPreset, result.SleepDelayPreset);
    }

    [Theory]
    [InlineData(1200u, 2100u)]
    [InlineData(600u, 1200u)]
    public void InterpretPreservesRepresentableCustomValues(uint displaySeconds, uint sleepSeconds)
    {
        IdleTimeoutInterpretation result = IdleTimeoutPolicy.Interpret(displaySeconds, sleepSeconds);

        Assert.Equal(IdleTimeoutInterpretationKind.Custom, result.Kind);
        Assert.Equal(sleepSeconds - displaySeconds, result.DelaySeconds);
    }

    [Theory]
    [InlineData(1800u, 900u)]
    [InlineData(0u, 1800u)]
    public void InterpretMarksUnsupportedOrderingAsIndividuallyConfigured(
        uint displaySeconds,
        uint sleepSeconds)
    {
        IdleTimeoutInterpretation result = IdleTimeoutPolicy.Interpret(displaySeconds, sleepSeconds);

        Assert.True(result.IsIndividuallyConfigured);
        Assert.Null(result.DelaySeconds);
    }
}
