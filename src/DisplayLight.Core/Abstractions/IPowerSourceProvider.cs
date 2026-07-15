using DisplayLight.Core.Power;

namespace DisplayLight.Core.Abstractions;

/// <summary>
/// Reports the computer's current AC or battery state.
/// </summary>
public interface IPowerSourceProvider
{
    PowerSource GetCurrent();
}
