using DisplayLight.Core.Power;

namespace DisplayLight.App.Presentation;

internal static class PresetPresentation
{
    internal static string GetLabel(DisplayTimeoutPreset preset) => preset switch
    {
        DisplayTimeoutPreset.OneMinute => "1分",
        DisplayTimeoutPreset.FiveMinutes => "5分",
        DisplayTimeoutPreset.TenMinutes => "10分",
        DisplayTimeoutPreset.ThirtyMinutes => "30分",
        DisplayTimeoutPreset.SixtyMinutes => "60分",
        DisplayTimeoutPreset.Never => "無期限",
        _ => "不明",
    };

    internal static string FormatCurrent(uint seconds)
    {
        if (DisplayTimeoutCatalog.TryFromSeconds(seconds, out DisplayTimeoutPreset preset))
        {
            return GetLabel(preset);
        }

        return seconds % 60 == 0
            ? $"{seconds / 60}分（プリセット外）"
            : $"{seconds}秒（プリセット外）";
    }
}
