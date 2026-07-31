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

    internal static string GetLabel(SleepDelayPreset preset) => preset switch
    {
        SleepDelayPreset.SameTime => "同時",
        SleepDelayPreset.FiveMinutes => "5分後",
        SleepDelayPreset.FifteenMinutes => "15分後",
        SleepDelayPreset.ThirtyMinutes => "30分後",
        SleepDelayPreset.SixtyMinutes => "60分後",
        SleepDelayPreset.Never => "スリープしない",
        _ => "不明",
    };

    internal static string FormatDuration(uint seconds)
    {
        if (DisplayTimeoutCatalog.TryFromSeconds(seconds, out DisplayTimeoutPreset preset))
        {
            return GetLabel(preset);
        }

        return FormatSeconds(seconds);
    }

    internal static string FormatSeconds(uint seconds) => seconds % 60 == 0
        ? $"{seconds / 60}分"
        : $"{seconds}秒";

    internal static string FormatCurrent(IdleTimeoutInterpretation interpretation)
    {
        if (interpretation.IsIndividuallyConfigured)
        {
            return "Windowsで個別に設定";
        }

        string displayText = interpretation.DisplayPreset == DisplayTimeoutPreset.Unknown
            ? FormatSeconds(interpretation.DisplaySeconds)
            : GetLabel(interpretation.DisplayPreset);
        string sleepText;
        if (interpretation.SleepDelayPreset == SleepDelayPreset.Never)
        {
            sleepText = "スリープしない";
        }
        else
        {
            string delayText = interpretation.SleepDelayPreset == SleepDelayPreset.Unknown
                ? $"{FormatSeconds(interpretation.DelaySeconds!.Value)}後"
                : GetLabel(interpretation.SleepDelayPreset);
            sleepText = $"{delayText}（合計{FormatSeconds(interpretation.SleepSeconds)}）";
        }

        string customSuffix = interpretation.Kind == IdleTimeoutInterpretationKind.Custom
            ? "（プリセット外）"
            : string.Empty;
        return $"画面{displayText}、PCは{sleepText}{customSuffix}";
    }
}
