using System.Runtime.InteropServices;

namespace DisplayLight.App.Infrastructure.Tray;

#pragma warning disable SYSLIB1054
internal static class TrayNativeMethods
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShellNotifyIcon(NotifyIconMessage message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterWindowMessageW", SetLastError = true)]
    internal static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadIconW", SetLastError = true)]
    internal static extern nint LoadIcon(nint instance, nint iconName);

    [DllImport("user32.dll", EntryPoint = "GetCursorPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPosition(out NativePoint point);
}
#pragma warning restore SYSLIB1054

internal enum NotifyIconMessage : uint
{
    Add = 0,
    Modify = 1,
    Delete = 2,
    SetFocus = 3,
    SetVersion = 4,
}

[Flags]
internal enum NotifyIconFlags : uint
{
    None = 0,
    Message = 0x00000001,
    Icon = 0x00000002,
    Tip = 0x00000004,
    ShowTip = 0x00000080,
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NotifyIconData
{
    internal uint Size;
    internal nint WindowHandle;
    internal uint Identifier;
    internal NotifyIconFlags Flags;
    internal uint CallbackMessage;
    internal nint IconHandle;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    internal string ToolTip;

    internal uint State;
    internal uint StateMask;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    internal string Information;

    internal uint Version;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    internal string InformationTitle;

    internal uint InformationFlags;
    internal Guid ItemGuid;
    internal nint BalloonIconHandle;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    internal int X;
    internal int Y;
}
