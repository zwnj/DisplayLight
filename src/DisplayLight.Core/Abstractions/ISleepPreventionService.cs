namespace DisplayLight.Core.Abstractions;

/// <summary>
/// Owns the process-scoped Windows request that prevents idle system sleep.
/// </summary>
public interface ISleepPreventionService : IDisposable
{
    bool IsActive { get; }

    void SetActive(bool isActive);

    void Renew();
}
