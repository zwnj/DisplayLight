using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DisplayLight.App.Infrastructure.Flyout;

internal static class FlyoutPositioner
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint SetWindowPositionNoActivate = 0x0010;
    private const uint SetWindowPositionNoSize = 0x0001;
    private const uint SetWindowPositionNoZOrder = 0x0004;

    internal static FlyoutWindowPlacement Calculate(
        Window window,
        NativeRectangle? iconBounds,
        double? desiredLogicalHeight = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        nint handle = new WindowInteropHelper(window).EnsureHandle();
        NativeRectangle anchor = iconBounds ?? GetFallbackAnchor();
        NativeRectangleInterop anchorInterop = NativeRectangleInterop.FromRectangle(anchor);
        nint monitor = NativeMethods.MonitorFromRectangle(in anchorInterop, MonitorDefaultToNearest);
        MonitorInformation monitorInformation = MonitorInformation.Create();
        if (monitor == nint.Zero || !NativeMethods.GetMonitorInformation(monitor, ref monitorInformation))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "フライアウトを表示するモニターを取得できませんでした。");
        }

        if (!window.IsVisible && !NativeMethods.SetWindowPosition(
                handle,
                nint.Zero,
                anchor.Left,
                anchor.Top,
                0,
                0,
                SetWindowPositionNoActivate | SetWindowPositionNoSize | SetWindowPositionNoZOrder))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "フライアウトの表示先DPIを取得する準備に失敗しました。");
        }

        uint dpi = NativeMethods.GetDpiForWindow(handle);

        if (dpi == 0)
        {
            dpi = 96;
        }

        NativeRectangle workArea = monitorInformation.WorkArea.ToRectangle();
        window.MaxHeight = Math.Max(320, (workArea.Height * 96d / dpi) - 24);
        double logicalWidth = double.IsNaN(window.Width) ? window.ActualWidth : window.Width;
        double logicalHeight;
        if (desiredLogicalHeight is double requestedHeight)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedHeight);
            logicalHeight = Math.Min(window.MaxHeight, requestedHeight);
        }
        else if (window.Content is FrameworkElement content)
        {
            content.Measure(new Size(logicalWidth, window.MaxHeight));
            logicalHeight = Math.Min(window.MaxHeight, content.DesiredSize.Height);
        }
        else
        {
            logicalHeight = window.ActualHeight;
        }

        int width = Math.Max(1, (int)Math.Ceiling(logicalWidth * dpi / 96d));
        int height = Math.Max(1, (int)Math.Ceiling(logicalHeight * dpi / 96d));
        NativeSize size = new(width, height);
        FlyoutPlacement placement = FlyoutPlacementCalculator.Calculate(anchor, workArea, size);
        return new FlyoutWindowPlacement(placement.Location, size, placement.Edge);
    }

    internal static void Move(Window window, FlyoutWindowPlacement placement, NativePoint location)
        => Move(window, location, placement.Size);

    internal static void Move(Window window, NativePoint location, NativeSize size)
    {
        ArgumentNullException.ThrowIfNull(window);

        nint handle = new WindowInteropHelper(window).EnsureHandle();

        if (!NativeMethods.SetWindowPosition(
                handle,
                nint.Zero,
                location.X,
                location.Y,
                size.Width,
                size.Height,
                SetWindowPositionNoActivate | SetWindowPositionNoZOrder))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "フライアウトを通知領域の近くへ配置できませんでした。");
        }
    }

    internal static void BeginResizeSurfaceBackground(Window window, Brush surfaceBrush)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(surfaceBrush);

        nint handle = new WindowInteropHelper(window).EnsureHandle();
        if (HwndSource.FromHwnd(handle) is not HwndSource source)
        {
            return;
        }

        source.CompositionTarget.BackgroundColor = GetOpaqueSurfaceColor(surfaceBrush);
    }

    internal static void EndResizeSurfaceBackground(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        nint handle = new WindowInteropHelper(window).EnsureHandle();
        if (HwndSource.FromHwnd(handle) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }
    }

    internal static Color GetOpaqueSurfaceColor(Brush surfaceBrush)
    {
        ArgumentNullException.ThrowIfNull(surfaceBrush);

        Color color = surfaceBrush is SolidColorBrush solidColorBrush
            ? solidColorBrush.Color
            : SystemColors.WindowColor;
        return Color.FromRgb(color.R, color.G, color.B);
    }

    internal static void ApplyWindowAppearance(Window window)
    {
        nint handle = new WindowInteropHelper(window).EnsureHandle();
        if (HwndSource.FromHwnd(handle) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        int roundedCornerPreference = 2;
        _ = NativeMethods.SetDwmWindowAttribute(handle, 33, ref roundedCornerPreference, sizeof(int));

        // 固定HWND内で前景面だけを移動するため、DWM枠を残すと完成位置へ
        // 白い外周だけが先行表示される。外周線はWPFのFlyoutSurfaceが描画する。
        int borderColor = unchecked((int)0xFFFFFFFE);
        _ = NativeMethods.SetDwmWindowAttribute(handle, 34, ref borderColor, sizeof(int));

        // 独自の位置アニメーションとDWM既定の表示遷移が重なると、背景面だけが
        // 別のウィンドウのように先行表示されるため、既定遷移は使用しない。
        int transitionsForcedDisabled = 1;
        _ = NativeMethods.SetDwmWindowAttribute(handle, 3, ref transitionsForcedDisabled, sizeof(int));

        int backdropType = SystemParameters.HighContrast ? 1 : 3;
        _ = NativeMethods.SetDwmWindowAttribute(handle, 38, ref backdropType, sizeof(int));

        WindowMargins margins = new(-1);
        _ = NativeMethods.ExtendFrameIntoClientArea(handle, ref margins);
    }

    internal static bool TrySetCloaked(Window window, bool cloaked)
    {
        ArgumentNullException.ThrowIfNull(window);

        nint handle = new WindowInteropHelper(window).EnsureHandle();
        int value = cloaked ? 1 : 0;
        return NativeMethods.SetDwmWindowAttribute(handle, 13, ref value, sizeof(int)) >= 0;
    }

    internal static void SetBackdropEnabled(Window window, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(window);

        nint handle = new WindowInteropHelper(window).EnsureHandle();
        int backdropType = enabled && !SystemParameters.HighContrast ? 3 : 1;
        _ = NativeMethods.SetDwmWindowAttribute(handle, 38, ref backdropType, sizeof(int));
    }

    internal static void ApplyTheme(Window window, bool useLightTheme)
    {
        nint handle = new WindowInteropHelper(window).EnsureHandle();
        int useImmersiveDarkMode = !useLightTheme && !SystemParameters.HighContrast ? 1 : 0;
        _ = NativeMethods.SetDwmWindowAttribute(handle, 20, ref useImmersiveDarkMode, sizeof(int));

        int backdropType = SystemParameters.HighContrast ? 1 : 3;
        _ = NativeMethods.SetDwmWindowAttribute(handle, 38, ref backdropType, sizeof(int));
    }

    private static NativeRectangle GetFallbackAnchor()
    {
        Rect workArea = SystemParameters.WorkArea;
        PresentationSource? source = PresentationSource.FromVisual(Application.Current.MainWindow);
        Matrix transform = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        Point bottomRight = transform.Transform(new Point(workArea.Right, workArea.Bottom));
        return new NativeRectangle(
            (int)bottomRight.X - 1,
            (int)bottomRight.Y - 1,
            (int)bottomRight.X,
            (int)bottomRight.Y);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", EntryPoint = "MonitorFromRect")]
        internal static extern nint MonitorFromRectangle(in NativeRectangleInterop rectangle, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInformation(nint monitor, ref MonitorInformation information);

        [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
        internal static extern uint GetDpiForWindow(nint window);

        [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPosition(
            nint window,
            nint insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
        internal static extern int SetDwmWindowAttribute(nint window, int attribute, ref int value, int valueSize);

        [DllImport("dwmapi.dll", EntryPoint = "DwmExtendFrameIntoClientArea")]
        internal static extern int ExtendFrameIntoClientArea(nint window, ref WindowMargins margins);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct WindowMargins
    {
        private readonly int Left;
        private readonly int Right;
        private readonly int Top;
        private readonly int Bottom;

        internal WindowMargins(int value)
        {
            Left = value;
            Right = value;
            Top = value;
            Bottom = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInformation
    {
        internal uint Size;
        internal NativeRectangleInterop Monitor;
        internal NativeRectangleInterop WorkArea;
        internal uint Flags;

        internal static MonitorInformation Create() => new()
        {
            Size = (uint)Marshal.SizeOf<MonitorInformation>(),
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRectangleInterop
    {
        internal readonly int Left;
        internal readonly int Top;
        internal readonly int Right;
        internal readonly int Bottom;

        internal NativeRectangle ToRectangle() => new(Left, Top, Right, Bottom);

        internal static NativeRectangleInterop FromRectangle(NativeRectangle rectangle) =>
            new(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

        private NativeRectangleInterop(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }
}

internal readonly record struct FlyoutWindowPlacement(
    NativePoint Location,
    NativeSize Size,
    TaskbarEdge Edge);
