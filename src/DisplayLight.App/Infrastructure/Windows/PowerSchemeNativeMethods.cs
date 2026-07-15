using System.Runtime.InteropServices;

namespace DisplayLight.App.Infrastructure.Windows;

internal static partial class PowerSchemeNativeMethods
{
    [LibraryImport("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
    internal static partial uint PowerGetActiveScheme(nint userRootPowerKey, out nint activePolicyGuid);

    [LibraryImport("powrprof.dll", EntryPoint = "PowerReadACValueIndex")]
    internal static partial uint PowerReadAcValueIndex(
        nint rootPowerKey,
        in Guid schemeGuid,
        in Guid subgroupGuid,
        in Guid settingGuid,
        out uint value);

    [LibraryImport("powrprof.dll", EntryPoint = "PowerReadDCValueIndex")]
    internal static partial uint PowerReadDcValueIndex(
        nint rootPowerKey,
        in Guid schemeGuid,
        in Guid subgroupGuid,
        in Guid settingGuid,
        out uint value);

    [LibraryImport("powrprof.dll", EntryPoint = "PowerWriteACValueIndex")]
    internal static partial uint PowerWriteAcValueIndex(
        nint rootPowerKey,
        in Guid schemeGuid,
        in Guid subgroupGuid,
        in Guid settingGuid,
        uint value);

    [LibraryImport("powrprof.dll", EntryPoint = "PowerWriteDCValueIndex")]
    internal static partial uint PowerWriteDcValueIndex(
        nint rootPowerKey,
        in Guid schemeGuid,
        in Guid subgroupGuid,
        in Guid settingGuid,
        uint value);

    [LibraryImport("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
    internal static partial uint PowerSetActiveScheme(nint userRootPowerKey, in Guid schemeGuid);

    [LibraryImport("kernel32.dll", EntryPoint = "LocalFree")]
    internal static partial nint LocalFree(nint memory);
}
