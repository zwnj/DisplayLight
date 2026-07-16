using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DisplayLight.App.Infrastructure.Flyout;
using DisplayLight.App.Presentation;
using DisplayLight.Core.Power;

namespace DisplayLight.App;

public partial class MainWindow : Window
{
    private bool isCloseAllowed;
    private bool isAuxiliaryMenuOpen;
    private bool isClosing;
    private int activeMotionCount;
    private bool isPositioning;
    private bool useLightTheme = true;
    private CancellationTokenSource? motionCancellation;
    private FlyoutWindowPlacement? currentPlacement;
    private NativePoint? currentWindowLocation;
    private NativeRectangle? lastIconBounds;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += HandleSourceInitialized;
        SizeChanged += HandleSizeChanged;
    }

    public event EventHandler? ExitRequested;

    public event EventHandler? FlyoutHidden;

    internal void ShowAt(NativeRectangle? iconBounds, bool focusPrimaryAction) =>
        _ = ShowAtAsync(iconBounds, focusPrimaryAction);

    private async Task ShowAtAsync(NativeRectangle? iconBounds, bool focusPrimaryAction)
    {
        lastIconBounds = iconBounds;
        bool wasVisible = IsVisible;
        CancelMotion();
        isClosing = false;
        CancellationTokenSource cancellation = new();
        motionCancellation = cancellation;

        try
        {
            isPositioning = true;
            UpdateLayout();
            FlyoutWindowPlacement placement = FlyoutPositioner.Calculate(this, lastIconBounds);
            currentPlacement = placement;

            bool shouldAnimate = SystemParameters.ClientAreaAnimation;
            NativePoint start = wasVisible && currentWindowLocation is NativePoint visibleLocation
                ? visibleLocation
                : FlyoutMotionCalculator.OffsetTowardsTaskbar(
                    placement.Location,
                    placement.Edge,
                    FlyoutMotionCalculator.OpeningDistanceDips,
                    placement.DpiScale);
            double startOpacity = wasVisible ? Opacity : 0;

            FlyoutPositioner.Move(this, placement, shouldAnimate ? start : placement.Location);
            currentWindowLocation = shouldAnimate ? start : placement.Location;
            Opacity = shouldAnimate ? startOpacity : 1;
            if (!IsVisible)
            {
                Show();
            }

            _ = Activate();
            if (focusPrimaryAction)
            {
                _ = SleepToggleButton.Focus();
            }

            isPositioning = false;

            if (shouldAnimate)
            {
                await AnimateWindowAsync(
                    placement,
                    start,
                    placement.Location,
                    startOpacity,
                    1,
                    TimeSpan.FromMilliseconds(160),
                    cancellation.Token);
            }

            if (!cancellation.IsCancellationRequested)
            {
                FlyoutPositioner.Move(this, placement, placement.Location);
                currentWindowLocation = placement.Location;
                Opacity = 1;
            }
        }
        catch (Win32Exception)
        {
            if (!IsVisible)
            {
                Show();
            }

            PositionFallback();
            Opacity = 1;
        }
        finally
        {
            isPositioning = false;
            if (ReferenceEquals(motionCancellation, cancellation))
            {
                motionCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void PositionFlyout()
    {
        if (isPositioning || activeMotionCount > 0)
        {
            return;
        }

        isPositioning = true;
        try
        {
            UpdateLayout();
            currentPlacement = FlyoutPositioner.Position(this, lastIconBounds);
            currentWindowLocation = currentPlacement.Value.Location;
        }
        catch (Win32Exception)
        {
            PositionFallback();
        }
        finally
        {
            isPositioning = false;
        }
    }

    internal void ToggleAt(NativeRectangle? iconBounds, bool focusPrimaryAction)
    {
        if (IsVisible && !isClosing)
        {
            HideFlyout();
            return;
        }

        ShowAt(iconBounds, focusPrimaryAction);
    }

    public void AllowClose() => isCloseAllowed = true;

    internal void ApplyTheme(bool useLight)
    {
        useLightTheme = useLight;
        FlyoutPositioner.ApplyTheme(this, useLightTheme);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!isCloseAllowed)
        {
            e.Cancel = true;
            HideFlyout();
        }
        else
        {
            CancelMotion();
        }

        base.OnClosing(e);
    }

    private void HideFlyout() => _ = HideFlyoutAsync();

    private async Task HideFlyoutAsync()
    {
        if (!IsVisible || isClosing)
        {
            return;
        }

        isClosing = true;
        CancelMotion();
        CancellationTokenSource cancellation = new();
        motionCancellation = cancellation;

        try
        {
            if (SystemParameters.ClientAreaAnimation && currentPlacement is FlyoutWindowPlacement placement)
            {
                NativePoint start = currentWindowLocation ?? placement.Location;
                NativePoint end = FlyoutMotionCalculator.OffsetTowardsTaskbar(
                    placement.Location,
                    placement.Edge,
                    FlyoutMotionCalculator.ClosingDistanceDips,
                    placement.DpiScale);
                await AnimateWindowAsync(
                    placement,
                    start,
                    end,
                    Opacity,
                    0,
                    TimeSpan.FromMilliseconds(100),
                    cancellation.Token);
            }

            if (!cancellation.IsCancellationRequested)
            {
                CompleteHide();
            }
        }
        catch (Win32Exception)
        {
            if (!cancellation.IsCancellationRequested)
            {
                CompleteHide();
            }
        }
        finally
        {
            if (ReferenceEquals(motionCancellation, cancellation))
            {
                motionCancellation = null;
                isClosing = false;
            }

            cancellation.Dispose();
        }
    }

    private Task AnimateWindowAsync(
        FlyoutWindowPlacement placement,
        NativePoint start,
        NativePoint end,
        double startOpacity,
        double endOpacity,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource completion = new();
        Stopwatch stopwatch = Stopwatch.StartNew();
        DispatcherTimer timer = new(DispatcherPriority.Render, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };

        activeMotionCount++;
        timer.Tick += HandleTick;
        timer.Start();
        return completion.Task;

        void HandleTick(object? sender, EventArgs e)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Complete();
                return;
            }

            double progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
            try
            {
                NativePoint location = FlyoutMotionCalculator.Interpolate(start, end, progress);
                FlyoutPositioner.Move(this, placement, location);
                currentWindowLocation = location;
                Opacity = startOpacity + ((endOpacity - startOpacity) * progress);
            }
            catch (Exception exception) when (exception is Win32Exception)
            {
                Complete(exception);
                return;
            }

            if (progress >= 1)
            {
                Complete(exception: null);
            }
        }

        void Complete(Exception? exception = null)
        {
            timer.Stop();
            timer.Tick -= HandleTick;
            stopwatch.Stop();
            activeMotionCount--;
            if (exception is null)
            {
                completion.TrySetResult();
            }
            else
            {
                completion.TrySetException(exception);
            }
        }
    }

    private void CancelMotion() => motionCancellation?.Cancel();

    private void PositionFallback()
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 12, workArea.Right - ActualWidth - 12);
        Top = Math.Max(workArea.Top + 12, workArea.Bottom - ActualHeight - 12);
        currentPlacement = null;
        currentWindowLocation = null;
    }

    private void CompleteHide()
    {
        Hide();
        Opacity = 1;
        currentWindowLocation = currentPlacement?.Location;
        FlyoutHidden?.Invoke(this, EventArgs.Empty);
    }

    private void HandleSourceInitialized(object? sender, EventArgs e)
    {
        FlyoutPositioner.ApplyWindowAppearance(this);
        FlyoutPositioner.ApplyTheme(this, useLightTheme);
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsVisible || isPositioning)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, PositionFlyout);
    }

    private async void Window_Deactivated(object sender, EventArgs e)
    {
        await Task.Delay(120);
        if (!isAuxiliaryMenuOpen && !IsActive && IsVisible)
        {
            HideFlyout();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        HideFlyout();
    }

    private void ExpandAc_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Expand(PowerSettingTarget.AcPower);
        }
    }

    private void ExpandBattery_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Expand(PowerSettingTarget.Battery);
        }
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is not ContextMenu menu)
        {
            return;
        }

        menu.PlacementTarget = MoreButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void AuxiliaryMenu_Opened(object sender, RoutedEventArgs e) => isAuxiliaryMenuOpen = true;

    private void AuxiliaryMenu_Closed(object sender, RoutedEventArgs e)
    {
        isAuxiliaryMenuOpen = false;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (!IsActive && IsVisible)
            {
                HideFlyout();
            }
        });
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) =>
        ExitRequested?.Invoke(this, EventArgs.Empty);
}
