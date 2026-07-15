using System.Runtime.InteropServices;

namespace DisplayLight.App.Infrastructure.Windows;

internal static partial class PowerRequestNativeMethods
{
    [LibraryImport("kernel32.dll", EntryPoint = "PowerCreateRequest", SetLastError = true)]
    internal static partial nint PowerCreateRequest(in PowerRequestReasonContext context);

    [LibraryImport("kernel32.dll", EntryPoint = "PowerSetRequest", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PowerSetRequest(SafePowerRequestHandle handle, PowerRequestType requestType);

    [LibraryImport("kernel32.dll", EntryPoint = "PowerClearRequest", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PowerClearRequest(SafePowerRequestHandle handle, PowerRequestType requestType);

    [LibraryImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct PowerRequestReasonContext(uint version, uint flags, nint simpleReasonString)
{
    internal readonly uint Version = version;
    internal readonly uint Flags = flags;
    internal readonly nint SimpleReasonString = simpleReasonString;
}

internal enum PowerRequestType
{
    DisplayRequired = 0,
    SystemRequired = 1,
    AwayModeRequired = 2,
    ExecutionRequired = 3,
}

internal sealed class SafePowerRequestHandle : SafeHandle
{
    internal SafePowerRequestHandle(nint handle)
        : base(nint.Zero, true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle is 0 or -1;

    protected override bool ReleaseHandle() => PowerRequestNativeMethods.CloseHandle(handle);
}
