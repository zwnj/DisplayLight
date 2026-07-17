using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private bool isContentResizePending;
    private bool isContentResizeQueued;
    private int activeMotionCount;
    private bool isPositioning;
    private bool useLightTheme = true;
    private CancellationTokenSource? motionCancellation;
    private CancellationTokenSource? sizeMotionCancellation;
    private FlyoutWindowPlacement? currentPlacement;
    private NativePoint? currentWindowLocation;
    private NativeSize? currentWindowSize;
    private NativeRectangle? lastIconBounds;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += HandleSourceInitialized;
        FlyoutContentPanel.SizeChanged += HandleContentSizeChanged;
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
            SizeToContent = System.Windows.SizeToContent.Manual;

            bool shouldAnimate = SystemParameters.ClientAreaAnimation;
            if (!wasVisible)
            {
                FlyoutContent.Opacity = shouldAnimate ? 0 : 1;
                FlyoutContent.IsHitTestVisible = !shouldAnimate;
            }

            NativePoint start = wasVisible && currentWindowLocation is NativePoint visibleLocation
                ? visibleLocation
                : FlyoutMotionCalculator.OffsetTowardsTaskbar(
                    placement.Location,
                    placement.Edge,
                    FlyoutMotionCalculator.CalculateHiddenDistance(placement.Size, placement.Edge));

            FlyoutPositioner.Move(this, placement, shouldAnimate ? start : placement.Location);
            currentWindowLocation = shouldAnimate ? start : placement.Location;
            currentWindowSize = placement.Size;
            Opacity = 1;
            if (!IsVisible)
            {
                Show();
            }

            _ = Activate();
            isPositioning = false;

            if (shouldAnimate)
            {
                await AnimateWindowAsync(
                    placement,
                    start,
                    placement.Location,
                    isOpening: true,
                    TimeSpan.FromMilliseconds(FlyoutMotionCalculator.OpeningDurationMilliseconds),
                    cancellation.Token);
            }

            if (!cancellation.IsCancellationRequested)
            {
                FlyoutPositioner.Move(this, placement, placement.Location);
                currentWindowLocation = placement.Location;
                currentWindowSize = placement.Size;
                Opacity = 1;

                if (shouldAnimate && FlyoutContent.Opacity < 1)
                {
                    await AnimateContentInAsync(
                        TimeSpan.FromMilliseconds(FlyoutMotionCalculator.ContentRevealDurationMilliseconds),
                        cancellation.Token);
                }

                if (!cancellation.IsCancellationRequested)
                {
                    FlyoutContent.IsHitTestVisible = true;
                    if (focusPrimaryAction)
                    {
                        _ = SleepToggleButton.Focus();
                    }
                }
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
            FlyoutContent.Opacity = 1;
            FlyoutContent.IsHitTestVisible = true;
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
        FlyoutContent.IsHitTestVisible = false;
        CancellationTokenSource cancellation = new();
        motionCancellation = cancellation;

        try
        {
            if (SystemParameters.ClientAreaAnimation && currentPlacement is FlyoutWindowPlacement placement)
            {
                NativePoint start = currentWindowLocation ?? placement.Location;
                NativeSize size = currentWindowSize ?? placement.Size;
                FlyoutWindowPlacement motionPlacement = placement with { Size = size };
                NativePoint end = FlyoutMotionCalculator.OffsetTowardsTaskbar(
                    start,
                    placement.Edge,
                    FlyoutMotionCalculator.CalculateHiddenDistance(size, placement.Edge));
                await AnimateWindowAsync(
                    motionPlacement,
                    start,
                    end,
                    isOpening: false,
                    TimeSpan.FromMilliseconds(FlyoutMotionCalculator.ClosingDurationMilliseconds),
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
        bool isOpening,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource completion = new();
        Stopwatch stopwatch = Stopwatch.StartNew();
        TimeSpan lastRenderingTime = TimeSpan.MinValue;

        activeMotionCount++;
        CompositionTarget.Rendering += HandleRendering;
        return completion.Task;

        void HandleRendering(object? sender, EventArgs e)
        {
            if (e is RenderingEventArgs renderingArgs)
            {
                if (renderingArgs.RenderingTime == lastRenderingTime)
                {
                    return;
                }

                lastRenderingTime = renderingArgs.RenderingTime;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Complete();
                return;
            }

            double progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
            try
            {
                NativePoint location = isOpening
                    ? FlyoutMotionCalculator.InterpolateOpening(start, end, progress)
                    : FlyoutMotionCalculator.InterpolateClosing(start, end, progress);
                FlyoutPositioner.Move(this, placement, location);
                currentWindowLocation = location;
                currentWindowSize = placement.Size;
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
            CompositionTarget.Rendering -= HandleRendering;
            stopwatch.Stop();
            activeMotionCount--;
            QueueContentResize();
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

    private Task AnimateContentInAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        TaskCompletionSource completion = new();
        Stopwatch stopwatch = Stopwatch.StartNew();
        TimeSpan lastRenderingTime = TimeSpan.MinValue;
        double startOpacity = FlyoutContent.Opacity;

        activeMotionCount++;
        CompositionTarget.Rendering += HandleRendering;
        return completion.Task;

        void HandleRendering(object? sender, EventArgs e)
        {
            if (e is RenderingEventArgs renderingArgs)
            {
                if (renderingArgs.RenderingTime == lastRenderingTime)
                {
                    return;
                }

                lastRenderingTime = renderingArgs.RenderingTime;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Complete();
                return;
            }

            double progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
            FlyoutContent.Opacity = FlyoutMotionCalculator.InterpolateContentOpacity(startOpacity, progress);
            if (progress >= 1)
            {
                FlyoutContent.Opacity = 1;
                Complete();
            }
        }

        void Complete()
        {
            CompositionTarget.Rendering -= HandleRendering;
            stopwatch.Stop();
            activeMotionCount--;
            QueueContentResize();
            completion.TrySetResult();
        }
    }

    private async Task AnimateBoundsAsync(
        NativePoint startLocation,
        NativeSize startSize,
        NativePoint endLocation,
        NativeSize endSize,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource completion = new();
        Stopwatch stopwatch = Stopwatch.StartNew();
        TimeSpan lastRenderingTime = TimeSpan.MinValue;

        activeMotionCount++;
        CompositionTarget.Rendering += HandleRendering;
        await completion.Task;

        void HandleRendering(object? sender, EventArgs e)
        {
            if (e is RenderingEventArgs renderingArgs)
            {
                if (renderingArgs.RenderingTime == lastRenderingTime)
                {
                    return;
                }

                lastRenderingTime = renderingArgs.RenderingTime;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Complete();
                return;
            }

            double progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
            try
            {
                NativePoint location = FlyoutMotionCalculator.InterpolateBoundsLocation(
                    startLocation,
                    endLocation,
                    progress);
                NativeSize size = FlyoutMotionCalculator.InterpolateBoundsSize(startSize, endSize, progress);
                FlyoutPositioner.Move(this, location, size);
                currentWindowLocation = location;
                currentWindowSize = size;
            }
            catch (Exception exception) when (exception is Win32Exception)
            {
                Complete(exception);
                return;
            }

            if (progress >= 1)
            {
                Complete();
            }
        }

        void Complete(Exception? exception = null)
        {
            CompositionTarget.Rendering -= HandleRendering;
            stopwatch.Stop();
            activeMotionCount--;
            QueueContentResize();
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

    private async Task ResizeFlyoutToContentAsync()
    {
        if (!IsVisible || isClosing || activeMotionCount > 0 || isPositioning)
        {
            return;
        }

        isContentResizePending = false;
        sizeMotionCancellation?.Cancel();
        CancellationTokenSource cancellation = new();
        sizeMotionCancellation = cancellation;
        bool isResizeBackgroundActive = false;

        try
        {
            UpdateLayout();
            FlyoutWindowPlacement target = FlyoutPositioner.Calculate(this, lastIconBounds);
            NativePoint startLocation = currentWindowLocation ?? target.Location;
            NativeSize startSize = currentWindowSize ?? target.Size;
            if (startLocation == target.Location && startSize == target.Size)
            {
                currentPlacement = target;
                return;
            }

            FlyoutContent.IsHitTestVisible = false;
            FlyoutPositioner.BeginResizeSurfaceBackground(this, FlyoutSurface.Background);
            isResizeBackgroundActive = true;
            if (SystemParameters.ClientAreaAnimation)
            {
                await AnimateBoundsAsync(
                    startLocation,
                    startSize,
                    target.Location,
                    target.Size,
                    TimeSpan.FromMilliseconds(FlyoutMotionCalculator.BoundsResizeDurationMilliseconds),
                    cancellation.Token);
            }

            if (!cancellation.IsCancellationRequested)
            {
                FlyoutPositioner.Move(this, target, target.Location);
                currentPlacement = target;
                currentWindowLocation = target.Location;
                currentWindowSize = target.Size;
                await WaitForNextRenderAsync();
                FlyoutContent.IsHitTestVisible = true;
            }
        }
        catch (Win32Exception)
        {
            if (!cancellation.IsCancellationRequested)
            {
                PositionFallback();
                FlyoutContent.IsHitTestVisible = true;
            }
        }
        finally
        {
            if (isResizeBackgroundActive)
            {
                FlyoutPositioner.EndResizeSurfaceBackground(this);
            }

            if (ReferenceEquals(sizeMotionCancellation, cancellation))
            {
                sizeMotionCancellation = null;
            }

            cancellation.Dispose();
            QueueContentResize();
        }
    }

    private static Task WaitForNextRenderAsync()
    {
        TaskCompletionSource completion = new();
        CompositionTarget.Rendering += HandleRendering;
        return completion.Task;

        void HandleRendering(object? sender, EventArgs e)
        {
            CompositionTarget.Rendering -= HandleRendering;
            completion.TrySetResult();
        }
    }

    private void CancelMotion()
    {
        motionCancellation?.Cancel();
        sizeMotionCancellation?.Cancel();
    }

    private void PositionFallback()
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 12, workArea.Right - ActualWidth - 12);
        Top = Math.Max(workArea.Top + 12, workArea.Bottom - ActualHeight - 12);
        currentPlacement = null;
        currentWindowLocation = null;
        currentWindowSize = null;
    }

    private void CompleteHide()
    {
        Hide();
        Opacity = 1;
        FlyoutContent.Opacity = 1;
        FlyoutContent.IsHitTestVisible = true;
        currentWindowLocation = currentPlacement?.Location;
        currentWindowSize = currentPlacement?.Size;
        isContentResizePending = false;
        FlyoutHidden?.Invoke(this, EventArgs.Empty);
    }

    private void HandleSourceInitialized(object? sender, EventArgs e)
    {
        FlyoutPositioner.ApplyWindowAppearance(this);
        FlyoutPositioner.ApplyTheme(this, useLightTheme);
    }

    private void HandleContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsVisible || isClosing || SizeToContent != System.Windows.SizeToContent.Manual)
        {
            return;
        }

        isContentResizePending = true;
        QueueContentResize();
    }

    private void QueueContentResize()
    {
        if (!isContentResizePending || isContentResizeQueued || !IsVisible || isClosing ||
            activeMotionCount > 0 || isPositioning)
        {
            return;
        }

        isContentResizeQueued = true;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            isContentResizeQueued = false;
            if (!isContentResizePending || !IsVisible || isClosing || activeMotionCount > 0 || isPositioning)
            {
                QueueContentResize();
                return;
            }

            _ = ResizeFlyoutToContentAsync();
        });
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
