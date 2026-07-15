using System.Runtime.InteropServices;

namespace DisplayLight.App.Infrastructure.Windows;

internal static partial class SystemPowerStatusNativeMethods
{
    [LibraryImport("kernel32.dll", EntryPoint = "GetSystemPowerStatus", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemPowerStatus(out SystemPowerStatus status);
}

[StructLayout(LayoutKind.Sequential)]
internal struct SystemPowerStatus
{
    internal byte AcLineStatus;
    internal byte BatteryFlag;
    internal byte BatteryLifePercent;
    internal byte SystemStatusFlag;
    internal uint BatteryLifeTime;
    internal uint BatteryFullLifeTime;
}
