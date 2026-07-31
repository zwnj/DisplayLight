namespace DisplayLight.Core.Power;

public enum IdleTimeoutInterpretationKind
{
    ExactPreset,
    Custom,
    IndividuallyConfigured,
}

/// <summary>
/// Describes how existing absolute Windows timeouts map to DisplayLight's relative model.
/// </summary>
public sealed record IdleTimeoutInterpretation(
    IdleTimeoutInterpretationKind Kind,
    uint DisplaySeconds,
    uint SleepSeconds,
    uint? DelaySeconds,
    DisplayTimeoutPreset DisplayPreset,
    SleepDelayPreset SleepDelayPreset)
{
    public bool IsIndividuallyConfigured =>
        Kind == IdleTimeoutInterpretationKind.IndividuallyConfigured;
}
