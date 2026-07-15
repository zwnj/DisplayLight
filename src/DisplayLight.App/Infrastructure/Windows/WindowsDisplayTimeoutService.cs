using DisplayLight.Core.Abstractions;
using DisplayLight.Core.Power;

namespace DisplayLight.App.Infrastructure.Windows;

internal sealed class WindowsDisplayTimeoutService(IPowerSchemeNativeApi nativeApi) : IDisplayTimeoutService
{
    public Task<DisplayTimeoutValues> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Read());
    }

    public Task<DisplayTimeoutValues> SetAsync(
        PowerSettingTarget target,
        DisplayTimeoutPreset preset,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Guid schemeGuid = nativeApi.GetActiveScheme();
        uint seconds = DisplayTimeoutCatalog.ToSeconds(preset);

        switch (target)
        {
            case PowerSettingTarget.AcPower:
                nativeApi.WriteAcValue(schemeGuid, seconds);
                break;
            case PowerSettingTarget.Battery:
                nativeApi.WriteDcValue(schemeGuid, seconds);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported power setting target.");
        }

        nativeApi.Activate(schemeGuid);
        DisplayTimeoutValues values = Read();
        uint actualSeconds = target == PowerSettingTarget.AcPower
            ? values.AcSeconds
            : values.BatterySeconds;

        if (actualSeconds != seconds)
        {
            throw new InvalidOperationException(
                $"ディスプレイ消灯時間を再確認したところ、要求値 {seconds} 秒に対して {actualSeconds} 秒でした。");
        }

        return Task.FromResult(values);
    }

    private DisplayTimeoutValues Read()
    {
        Guid schemeGuid = nativeApi.GetActiveScheme();
        return new(
            nativeApi.ReadAcValue(schemeGuid),
            nativeApi.ReadDcValue(schemeGuid));
    }
}
