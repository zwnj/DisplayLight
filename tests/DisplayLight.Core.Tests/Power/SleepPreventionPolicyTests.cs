using DisplayLight.Core.Power;

namespace DisplayLight.Core.Tests.Power;

public sealed class SleepPreventionPolicyTests
{
    [Fact]
    public void EvaluateReleasesRequestWhenNotRequested()
    {
        SleepPreventionDecision decision = SleepPreventionPolicy.Evaluate(
            isRequested: false,
            isAcPowerOnly: false,
            PowerSource.AcPower);

        Assert.False(decision.ShouldPreventSleep);
        Assert.Equal(SleepPreventionReason.NotRequested, decision.Reason);
    }

    [Theory]
    [InlineData(PowerSource.AcPower)]
    [InlineData(PowerSource.Battery)]
    [InlineData(PowerSource.Unknown)]
    public void EvaluateKeepsRequestWhenAcOnlyGuardIsDisabled(PowerSource source)
    {
        SleepPreventionDecision decision = SleepPreventionPolicy.Evaluate(
            isRequested: true,
            isAcPowerOnly: false,
            source);

        Assert.True(decision.ShouldPreventSleep);
        Assert.Equal(SleepPreventionReason.Active, decision.Reason);
    }

    [Fact]
    public void EvaluateKeepsAcOnlyRequestOnAcPower()
    {
        SleepPreventionDecision decision = SleepPreventionPolicy.Evaluate(
            isRequested: true,
            isAcPowerOnly: true,
            PowerSource.AcPower);

        Assert.True(decision.ShouldPreventSleep);
        Assert.Equal(SleepPreventionReason.Active, decision.Reason);
    }

    [Fact]
    public void EvaluateReleasesAcOnlyRequestOnBattery()
    {
        SleepPreventionDecision decision = SleepPreventionPolicy.Evaluate(
            isRequested: true,
            isAcPowerOnly: true,
            PowerSource.Battery);

        Assert.False(decision.ShouldPreventSleep);
        Assert.Equal(SleepPreventionReason.WaitingForAcPower, decision.Reason);
    }

    [Fact]
    public void EvaluateReleasesAcOnlyRequestWhenSourceIsUnknown()
    {
        SleepPreventionDecision decision = SleepPreventionPolicy.Evaluate(
            isRequested: true,
            isAcPowerOnly: true,
            PowerSource.Unknown);

        Assert.False(decision.ShouldPreventSleep);
        Assert.Equal(SleepPreventionReason.PowerSourceUnavailable, decision.Reason);
    }
}
