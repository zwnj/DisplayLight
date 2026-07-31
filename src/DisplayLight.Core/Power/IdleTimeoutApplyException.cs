namespace DisplayLight.Core.Power;

/// <summary>
/// Reports a failed paired timeout update together with the values observed after recovery.
/// </summary>
public sealed class IdleTimeoutApplyException(
    string message,
    IdleTimeoutValues? actualValues,
    bool restoreSucceeded,
    Exception innerException)
    : Exception(message, innerException)
{
    public IdleTimeoutValues? ActualValues { get; } = actualValues;

    public bool RestoreSucceeded { get; } = restoreSucceeded;
}
