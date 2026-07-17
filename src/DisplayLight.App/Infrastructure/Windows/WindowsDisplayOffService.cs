using DisplayLight.Core.Abstractions;

namespace DisplayLight.App.Infrastructure.Windows;

internal sealed class WindowsDisplayOffService : IDisplayOffService
{
    internal const uint SystemCommandMessage = 0x0112;
    internal const nuint MonitorPowerCommand = 0xF170;
    internal const nint PowerOff = 2;

    private readonly Func<nint> windowHandleProvider;
    private readonly IDisplayOffNativeApi nativeApi;

    public WindowsDisplayOffService(Func<nint> windowHandleProvider)
        : this(windowHandleProvider, new DisplayOffNativeMethods())
    {
    }

    internal WindowsDisplayOffService(Func<nint> windowHandleProvider, IDisplayOffNativeApi nativeApi)
    {
        this.windowHandleProvider = windowHandleProvider ?? throw new ArgumentNullException(nameof(windowHandleProvider));
        this.nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
    }

    public void TurnOff()
    {
        nint windowHandle = windowHandleProvider();
        if (windowHandle == nint.Zero)
        {
            throw new InvalidOperationException("ディスプレイ消灯要求を送るウィンドウを取得できませんでした。");
        }

        // SC_MONITORPOWER is a predefined system command. DefWindowProc performs it for
        // this app's own HWND without broadcasting a blocking message to other processes.
        _ = nativeApi.DefWindowProcedure(windowHandle, SystemCommandMessage, MonitorPowerCommand, PowerOff);
    }
}
