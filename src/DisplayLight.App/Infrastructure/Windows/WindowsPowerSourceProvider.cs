using System.ComponentModel;
using System.Runtime.InteropServices;
using DisplayLight.Core.Abstractions;
using DisplayLight.Core.Power;

namespace DisplayLight.App.Infrastructure.Windows;

internal sealed class WindowsPowerSourceProvider : IPowerSourceProvider
{
    public PowerSource GetCurrent()
    {
        if (!SystemPowerStatusNativeMethods.GetSystemPowerStatus(out SystemPowerStatus status))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "現在の電源状態を取得できませんでした。");
        }

        return status.AcLineStatus switch
        {
            0 => PowerSource.Battery,
            1 => PowerSource.AcPower,
            _ => PowerSource.Unknown,
        };
    }
}
