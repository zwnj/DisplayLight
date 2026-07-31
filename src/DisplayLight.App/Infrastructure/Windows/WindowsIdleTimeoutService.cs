using DisplayLight.Core.Abstractions;
using DisplayLight.Core.Power;

namespace DisplayLight.App.Infrastructure.Windows;

internal sealed class WindowsIdleTimeoutService(IPowerSchemeNativeApi nativeApi) : IIdleTimeoutService
{
    public Task<IdleTimeoutValues> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Guid schemeGuid = nativeApi.GetActiveScheme();
        return Task.FromResult(Read(schemeGuid));
    }

    public Task<IdleTimeoutValues> SetAsync(
        PowerSettingTarget target,
        IdleTimeoutSelection selection,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(selection);

        Guid schemeGuid = nativeApi.GetActiveScheme();
        IdleTimeoutPair previous = ReadTarget(schemeGuid, target);
        uint displaySeconds = DisplayTimeoutCatalog.ToSeconds(selection.DisplayTimeout);
        uint sleepSeconds = selection.ToSleepTimeoutSeconds();

        try
        {
            WriteTarget(schemeGuid, target, displaySeconds, sleepSeconds);
            nativeApi.Activate(schemeGuid);

            IdleTimeoutValues values = Read(schemeGuid);
            IdleTimeoutPair actual = values.For(target);
            if (actual.DisplaySeconds != displaySeconds || actual.SleepSeconds != sleepSeconds)
            {
                throw new InvalidOperationException(
                    "電源設定を再確認したところ、要求したディスプレイ消灯時間とシステムスリープ時間に一致しませんでした。");
            }

            return Task.FromResult(values);
        }
        catch (Exception exception)
        {
            bool restoreSucceeded = TryRestore(schemeGuid, target, previous);
            IdleTimeoutValues? actualValues = TryReadActualValues(schemeGuid);
            string recoveryText = restoreSucceeded
                ? "変更前の電源設定へ復旧しました。"
                : "変更前の電源設定へ完全には復旧できませんでした。現在値を確認してください。";
            throw new IdleTimeoutApplyException(
                $"{exception.Message} {recoveryText}",
                actualValues,
                restoreSucceeded,
                exception);
        }
    }

    private IdleTimeoutValues Read(Guid schemeGuid) =>
        new(
            nativeApi.ReadAcDisplayValue(schemeGuid),
            nativeApi.ReadAcSleepValue(schemeGuid),
            nativeApi.ReadDcDisplayValue(schemeGuid),
            nativeApi.ReadDcSleepValue(schemeGuid));

    private IdleTimeoutPair ReadTarget(Guid schemeGuid, PowerSettingTarget target) => target switch
    {
        PowerSettingTarget.AcPower => new(
            nativeApi.ReadAcDisplayValue(schemeGuid),
            nativeApi.ReadAcSleepValue(schemeGuid)),
        PowerSettingTarget.Battery => new(
            nativeApi.ReadDcDisplayValue(schemeGuid),
            nativeApi.ReadDcSleepValue(schemeGuid)),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported power setting target."),
    };

    private void WriteTarget(
        Guid schemeGuid,
        PowerSettingTarget target,
        uint displaySeconds,
        uint sleepSeconds)
    {
        switch (target)
        {
            case PowerSettingTarget.AcPower:
                nativeApi.WriteAcDisplayValue(schemeGuid, displaySeconds);
                nativeApi.WriteAcSleepValue(schemeGuid, sleepSeconds);
                break;
            case PowerSettingTarget.Battery:
                nativeApi.WriteDcDisplayValue(schemeGuid, displaySeconds);
                nativeApi.WriteDcSleepValue(schemeGuid, sleepSeconds);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported power setting target.");
        }
    }

    private bool TryRestore(
        Guid schemeGuid,
        PowerSettingTarget target,
        IdleTimeoutPair previous)
    {
        try
        {
            WriteTarget(schemeGuid, target, previous.DisplaySeconds, previous.SleepSeconds);
            nativeApi.Activate(schemeGuid);
            return ReadTarget(schemeGuid, target) == previous;
        }
        catch
        {
            return false;
        }
    }

    private IdleTimeoutValues? TryReadActualValues(Guid schemeGuid)
    {
        try
        {
            return Read(schemeGuid);
        }
        catch
        {
            return null;
        }
    }
}
