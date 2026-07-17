using System.Runtime.InteropServices;

namespace DisplayLight.App.Infrastructure.Windows;

internal sealed partial class DisplayOffNativeMethods : IDisplayOffNativeApi
{
    public nint DefWindowProcedure(nint windowHandle, uint message, nuint wParam, nint lParam) =>
        DefWindowProc(windowHandle, message, wParam, lParam);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static partial nint DefWindowProc(nint windowHandle, uint message, nuint wParam, nint lParam);
}
