namespace DisplayLight.Core.Power;

/// <summary>
/// Holds the user-cancellable countdown independently of UI timers and Windows APIs.
/// </summary>
public sealed class DisplayOffCountdown
{
    public const int InitialSeconds = 3;

    public int RemainingSeconds { get; private set; }

    public bool IsActive => RemainingSeconds > 0;

    public void Start() => RemainingSeconds = InitialSeconds;

    public void Cancel() => RemainingSeconds = 0;

    /// <summary>
    /// Advances the countdown and returns true exactly once when display-off should run.
    /// </summary>
    public bool Tick()
    {
        if (!IsActive)
        {
            return false;
        }

        RemainingSeconds--;
        return RemainingSeconds == 0;
    }
}
