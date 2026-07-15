namespace DisplayLight.Core.Power;

/// <summary>
/// Identifies which Windows power condition a display timeout belongs to.
/// </summary>
public enum PowerSettingTarget
{
    Unknown = 0,
    AcPower = 1,
    Battery = 2,
}
