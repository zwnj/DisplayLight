using DisplayLight.Core.Settings;

namespace DisplayLight.Core.Abstractions;

/// <summary>
/// Loads and atomically stores user preferences.
/// </summary>
public interface IUserSettingsStore
{
    Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default);
}
