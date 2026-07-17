using DisplayLight.App.Infrastructure.Windows;

namespace DisplayLight.App.Tests.Infrastructure.Windows;

public sealed class WindowsDisplayOffServiceTests
{
    [Fact]
    public void TurnOffSendsMonitorPowerOffToTheOwnedWindow()
    {
        FakeDisplayOffNativeApi nativeApi = new();
        WindowsDisplayOffService service = new(() => 42, nativeApi);

        service.TurnOff();

        Assert.Equal(42, nativeApi.WindowHandle);
        Assert.Equal(WindowsDisplayOffService.SystemCommandMessage, nativeApi.Message);
        Assert.Equal(WindowsDisplayOffService.MonitorPowerCommand, nativeApi.WParam);
        Assert.Equal(WindowsDisplayOffService.PowerOff, nativeApi.LParam);
    }

    [Fact]
    public void TurnOffRejectsMissingWindowHandle()
    {
        WindowsDisplayOffService service = new(() => nint.Zero, new FakeDisplayOffNativeApi());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(service.TurnOff);

        Assert.Contains("ウィンドウ", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FakeDisplayOffNativeApi : IDisplayOffNativeApi
    {
        public nint WindowHandle { get; private set; }

        public uint Message { get; private set; }

        public nuint WParam { get; private set; }

        public nint LParam { get; private set; }

        public nint DefWindowProcedure(nint windowHandle, uint message, nuint wParam, nint lParam)
        {
            WindowHandle = windowHandle;
            Message = message;
            WParam = wParam;
            LParam = lParam;
            return nint.Zero;
        }
    }
}
