using DisplayLight.Core.Power;

namespace DisplayLight.Core.Abstractions;

/// <summary>
/// Reads and changes the active Windows power scheme's display timeout values.
/// </summary>
public interface IDisplayTimeoutService
{
    Task<DisplayTimeoutValues> ReadAsync(CancellationToken cancellationToken = default);

    Task<DisplayTimeoutValues> SetAsync(
        PowerSettingTarget target,
        DisplayTimeoutPreset preset,
        CancellationToken cancellationToken = default);
}
