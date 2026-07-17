namespace DisplayLight.App.Infrastructure.Windows;

internal interface IDisplayOffNativeApi
{
    nint DefWindowProcedure(nint windowHandle, uint message, nuint wParam, nint lParam);
}
