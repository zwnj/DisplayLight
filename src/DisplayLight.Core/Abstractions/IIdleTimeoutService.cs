using DisplayLight.Core.Power;

namespace DisplayLight.Core.Abstractions;

/// <summary>
/// Reads and changes paired display and system sleep timeouts in the active power scheme.
/// </summary>
public interface IIdleTimeoutService
{
    Task<IdleTimeoutValues> ReadAsync(CancellationToken cancellationToken = default);

    Task<IdleTimeoutValues> SetAsync(
        PowerSettingTarget target,
        IdleTimeoutSelection selection,
        CancellationToken cancellationToken = default);
}
