namespace DisplayLight.Core.Abstractions;

/// <summary>
/// Performs a one-time request to turn off the attached displays.
/// </summary>
public interface IDisplayOffService
{
    void TurnOff();
}
