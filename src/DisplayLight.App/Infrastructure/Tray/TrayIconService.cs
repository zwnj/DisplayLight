using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DisplayLight.App.Infrastructure.Flyout;
using DisplayLight.App.Presentation;

namespace DisplayLight.App.Infrastructure.Tray;

internal sealed class TrayIconService : IDisposable
{
    private const uint IconIdentifier = 1;
    private const uint CallbackMessage = 0x8001;
    private const uint NotifyIconVersion4 = 4;
    private const uint NotifySelect = 0x0400;
    private const uint NotifyKeySelect = 0x0401;
    private const uint WindowContextMenu = 0x007B;
    private const uint WindowPowerBroadcast = 0x0218;
    private const uint PowerStatusChange = 0x000A;
    private const uint PowerResumeAutomatic = 0x0012;
    private const int DefaultApplicationIcon = 32512;
    private const int MaximumIconRestoreAttempts = 5;

    private readonly MainWindowViewModel viewModel;
    private readonly Action<TrayActivation> activateWindow;
    private readonly Action exitApplication;
    private readonly ContextMenu contextMenu;

    private HwndSource? windowSource;
    private nint windowHandle;
    private nint iconHandle;
    private uint taskbarCreatedMessage;
    private bool isAdded;
    private bool isDisposed;
    private DispatcherTimer? iconRestoreTimer;
    private int iconRestoreAttempts;

    public TrayIconService(
        MainWindowViewModel viewModel,
        Action<TrayActivation> activateWindow,
        Action exitApplication)
    {
        this.viewModel = viewModel;
        this.activateWindow = activateWindow;
        this.exitApplication = exitApplication;

        MenuItem openItem = new() { Header = "DisplayLightを開く" };
        openItem.Click += (_, _) => activateWindow(CreateActivation(isKeyboardInvocation: false));

        MenuItem exitItem = new() { Header = "終了" };
        exitItem.Click += (_, _) => exitApplication();

        contextMenu = new ContextMenu
        {
            Items =
            {
                openItem,
                new Separator(),
                exitItem,
            },
        };
        contextMenu.Closed += HandleContextMenuClosed;
    }

    public void Initialize(Window window)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentNullException.ThrowIfNull(window);

        windowHandle = new WindowInteropHelper(window).EnsureHandle();
        windowSource = HwndSource.FromHwnd(windowHandle)
            ?? throw new InvalidOperationException("通知領域に必要なウィンドウハンドルを取得できませんでした。");
        windowSource.AddHook(HandleWindowMessage);

        taskbarCreatedMessage = TrayNativeMethods.RegisterWindowMessage("TaskbarCreated");
        if (taskbarCreatedMessage == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "TaskbarCreatedメッセージを登録できませんでした。");
        }

        iconHandle = TrayNativeMethods.LoadIcon(nint.Zero, (nint)DefaultApplicationIcon);
        if (iconHandle == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "通知領域アイコンを読み込めませんでした。");
        }

        AddIcon();
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        contextMenu.Closed -= HandleContextMenuClosed;
        contextMenu.IsOpen = false;
        StopIconRestoreTimer();

        if (isAdded)
        {
            NotifyIconData data = CreateData(NotifyIconFlags.None);
            _ = TrayNativeMethods.ShellNotifyIcon(NotifyIconMessage.Delete, ref data);
            isAdded = false;
        }

        windowSource?.RemoveHook(HandleWindowMessage);
        windowSource = null;
        windowHandle = nint.Zero;
    }

    private void AddIcon()
    {
        NotifyIconData data = CreateData(
            NotifyIconFlags.Message |
            NotifyIconFlags.Icon |
            NotifyIconFlags.Tip |
            NotifyIconFlags.ShowTip);

        if (!TrayNativeMethods.ShellNotifyIcon(NotifyIconMessage.Add, ref data))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "通知領域アイコンを追加できませんでした。");
        }

        isAdded = true;
        data.Version = NotifyIconVersion4;
        if (!TrayNativeMethods.ShellNotifyIcon(NotifyIconMessage.SetVersion, ref data))
        {
            NotifyIconData deleteData = CreateData(NotifyIconFlags.None);
            _ = TrayNativeMethods.ShellNotifyIcon(NotifyIconMessage.Delete, ref deleteData);
            isAdded = false;
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "通知領域アイコンの動作モードを設定できませんでした。");
        }
    }

    private void UpdateToolTip()
    {
        if (!isAdded)
        {
            return;
        }

        NotifyIconData data = CreateData(NotifyIconFlags.Tip | NotifyIconFlags.ShowTip);
        _ = TrayNativeMethods.ShellNotifyIcon(NotifyIconMessage.Modify, ref data);
    }

    private NotifyIconData CreateData(NotifyIconFlags flags) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = windowHandle,
        Identifier = IconIdentifier,
        Flags = flags,
        CallbackMessage = CallbackMessage,
        IconHandle = iconHandle,
        ToolTip = LimitToolTip(viewModel.GetTrayToolTip()),
        Information = string.Empty,
        InformationTitle = string.Empty,
    };

    private nint HandleWindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        uint unsignedMessage = unchecked((uint)message);

        if (unsignedMessage == taskbarCreatedMessage)
        {
            isAdded = false;
            iconRestoreAttempts = 0;
            TryRestoreIcon();
            handled = true;
            return nint.Zero;
        }

        if (unsignedMessage == CallbackMessage)
        {
            uint eventCode = unchecked((uint)lParam.ToInt64()) & 0xFFFF;
            uint iconIdentifier = (unchecked((uint)lParam.ToInt64()) >> 16) & 0xFFFF;
            if (iconIdentifier != IconIdentifier)
            {
                return nint.Zero;
            }

            if (eventCode is NotifySelect or NotifyKeySelect)
            {
                activateWindow(CreateActivation(eventCode == NotifyKeySelect));
                handled = true;
            }
            else if (eventCode == WindowContextMenu)
            {
                OpenContextMenu();
                handled = true;
            }

            return nint.Zero;
        }

        if (unsignedMessage == WindowPowerBroadcast)
        {
            uint powerEvent = unchecked((uint)wParam.ToInt64());
            if (powerEvent == PowerResumeAutomatic)
            {
                viewModel.HandleSystemResume();
            }
            else if (powerEvent == PowerStatusChange && viewModel.RefreshCommand.CanExecute(parameter: null))
            {
                viewModel.RefreshCommand.Execute(parameter: null);
            }
        }

        return nint.Zero;
    }

    private void OpenContextMenu()
    {
        if (TrayNativeMethods.GetCursorPosition(out NativePoint point))
        {
            Point logicalPoint = new(point.X, point.Y);
            if (windowSource?.CompositionTarget is not null)
            {
                logicalPoint = windowSource.CompositionTarget.TransformFromDevice.Transform(logicalPoint);
            }

            contextMenu.Placement = PlacementMode.AbsolutePoint;
            contextMenu.HorizontalOffset = logicalPoint.X;
            contextMenu.VerticalOffset = logicalPoint.Y;
        }
        else
        {
            contextMenu.Placement = PlacementMode.MousePoint;
        }

        contextMenu.IsOpen = true;
    }

    private void TryRestoreIcon()
    {
        if (isDisposed)
        {
            return;
        }

        iconRestoreAttempts++;

        try
        {
            AddIcon();
            StopIconRestoreTimer();
        }
        catch (Win32Exception) when (iconRestoreAttempts < MaximumIconRestoreAttempts)
        {
            iconRestoreTimer ??= CreateIconRestoreTimer();
            iconRestoreTimer.Start();
        }
        catch (Win32Exception)
        {
            StopIconRestoreTimer();
            isAdded = false;
            activateWindow(CreateActivation(isKeyboardInvocation: false));
        }
    }

    private DispatcherTimer CreateIconRestoreTimer()
    {
        DispatcherTimer timer = new(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        timer.Tick += HandleIconRestoreTimerTick;
        return timer;
    }

    private void HandleIconRestoreTimerTick(object? sender, EventArgs e) => TryRestoreIcon();

    private void StopIconRestoreTimer()
    {
        if (iconRestoreTimer is null)
        {
            return;
        }

        iconRestoreTimer.Stop();
        iconRestoreTimer.Tick -= HandleIconRestoreTimerTick;
        iconRestoreTimer = null;
    }

    private void HandleContextMenuClosed(object sender, RoutedEventArgs e)
    {
        if (!isAdded)
        {
            return;
        }

        NotifyIconData data = CreateData(NotifyIconFlags.None);
        _ = TrayNativeMethods.ShellNotifyIcon(NotifyIconMessage.SetFocus, ref data);
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.IsSleepPreventionActive)
            or nameof(MainWindowViewModel.IsSleepPreventionRequested))
        {
            UpdateToolTip();
        }
    }

    private static string LimitToolTip(string value) => value.Length < 128 ? value : value[..127];

    internal void RestoreNotificationAreaFocus()
    {
        if (!isAdded)
        {
            return;
        }

        NotifyIconData data = CreateData(NotifyIconFlags.None);
        _ = TrayNativeMethods.ShellNotifyIcon(NotifyIconMessage.SetFocus, ref data);
    }

    internal NativeRectangle? TryGetIconBounds()
    {
        NotifyIconIdentifier identifier = new()
        {
            Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            WindowHandle = windowHandle,
            Identifier = IconIdentifier,
        };

        return TrayNativeMethods.GetNotifyIconRectangle(in identifier, out NativeRectangleInterop rectangle) >= 0
            ? rectangle.ToRectangle()
            : null;
    }

    private TrayActivation CreateActivation(bool isKeyboardInvocation) =>
        new(TryGetIconBounds(), isKeyboardInvocation);
}

internal readonly record struct TrayActivation(NativeRectangle? IconBounds, bool IsKeyboardInvocation);
