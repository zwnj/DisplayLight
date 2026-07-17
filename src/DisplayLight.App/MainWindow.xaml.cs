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
    private bool isMotionBackdropSuspended;
    private bool useLightTheme = true;
    private CancellationTokenSource? motionCancellation;
    private CancellationTokenSource? sizeMotionCancellation;
    private FlyoutWindowPlacement? currentPlacement;
    private NativePoint? currentWindowLocation;
    private NativeSize? currentWindowSize;
    private NativeRectangle? lastIconBounds;
    private MainWindowViewModel? observedViewModel;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += HandleSourceInitialized;
        DataContextChanged += HandleDataContextChanged;
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
        bool openingCloaked = false;

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
                FlyoutContent.Opacity = shouldAnimate ? FlyoutMotionCalculator.OpeningContentOpacity : 1;
                FlyoutContent.IsHitTestVisible = !shouldAnimate;
                if (shouldAnimate)
                {
                    BeginSurfaceMotionAppearance();
                }

                openingCloaked = shouldAnimate && FlyoutPositioner.TrySetCloaked(this, true);
                Opacity = shouldAnimate && !openingCloaked ? 0 : 1;
            }

            FlyoutPositioner.Move(this, placement, placement.Location);
            currentWindowLocation = placement.Location;
            currentWindowSize = placement.Size;
            if (!IsVisible)
            {
                Show();
            }

            if (!wasVisible && shouldAnimate)
            {
                UpdateLayout();
                SetSurfaceOffset(FlyoutMotionCalculator.CalculateHiddenSurfaceOffset(
                    FlyoutSurface.ActualWidth,
                    FlyoutSurface.ActualHeight,
                    placement.Edge));
                await PrepareOpeningSurfaceAsync(cancellation.Token);
                if (cancellation.IsCancellationRequested)
                {
                    return;
                }

                Opacity = 1;
                if (openingCloaked)
                {
                    _ = FlyoutPositioner.TrySetCloaked(this, false);
                    openingCloaked = false;
                }
            }

            _ = Activate();
            isPositioning = false;

            if (shouldAnimate)
            {
                await AnimateSurfaceAsync(
                    placement.Edge,
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
                EndSurfaceMotionAppearance();

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
            EndSurfaceMotionAppearance();
            SetSurfaceOffset(SurfaceOffset.Zero);
            if (openingCloaked)
            {
                _ = FlyoutPositioner.TrySetCloaked(this, false);
                openingCloaked = false;
            }

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
        switch (FlyoutToggleActionCalculator.Calculate(IsVisible, isClosing))
        {
            case FlyoutToggleAction.Hide:
                HideFlyout();
                break;
            case FlyoutToggleAction.Show:
                ShowAt(iconBounds, focusPrimaryAction);
                break;
            case FlyoutToggleAction.Ignore:
                // Deactivated が先に閉じ始めた後、同じクリックの通知領域イベントが届く場合がある。
                // ここで再表示すると一回のクリックが閉じる操作と開く操作の両方になる。
                break;
        }
    }

    public void AllowClose() => isCloseAllowed = true;

    internal void ApplyTheme(bool useLight)
    {
        useLightTheme = useLight;
        FlyoutPositioner.ApplyTheme(this, useLightTheme);
        if (isMotionBackdropSuspended)
        {
            FlyoutPositioner.SetBackdropEnabled(this, false);
        }
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

    private void HideFlyout()
    {
        observedViewModel?.CancelDisplayOffCountdown();
        _ = HideFlyoutAsync();
    }

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
                BeginSurfaceMotionAppearance();
                await AnimateSurfaceAsync(
                    placement.Edge,
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

    private Task AnimateSurfaceAsync(
        TaskbarEdge edge,
        bool isOpening,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource completion = new();
        Stopwatch stopwatch = Stopwatch.StartNew();
        TimeSpan lastRenderingTime = TimeSpan.MinValue;
        SurfaceOffset start = new(FlyoutSurfaceTranslation.X, FlyoutSurfaceTranslation.Y);
        SurfaceOffset hidden = FlyoutMotionCalculator.CalculateHiddenSurfaceOffset(
            FlyoutSurface.ActualWidth,
            FlyoutSurface.ActualHeight,
            edge);
        SurfaceOffset end = isOpening ? SurfaceOffset.Zero : hidden;

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
            SurfaceOffset offset = isOpening
                ? FlyoutMotionCalculator.InterpolateOpening(start, end, progress)
                : FlyoutMotionCalculator.InterpolateClosing(start, end, progress);
            SetSurfaceOffset(offset);

            if (progress >= 1)
            {
                SetSurfaceOffset(end);
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
        bool isInputSuppressed = false;

        try
        {
            UpdateLayout();
            double desiredLogicalHeight = MeasureDesiredSurfaceHeight();
            FlyoutWindowPlacement target = FlyoutPositioner.Calculate(
                this,
                lastIconBounds,
                desiredLogicalHeight);
            NativePoint startLocation = currentWindowLocation ?? target.Location;
            NativeSize startSize = currentWindowSize ?? target.Size;
            if (startLocation == target.Location && startSize == target.Size)
            {
                currentPlacement = target;
                return;
            }

            FlyoutContent.IsHitTestVisible = false;
            isInputSuppressed = true;
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
                await SynchronizeLayoutAfterResizeAsync();
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
            // 保留された再計測や非表示操作が伸縮を中断しても、入力停止だけは残さない。
            // 開き直すまで全ボタンが反応しなくなるため、成功経路ではなく finally で復元する。
            if (isInputSuppressed)
            {
                FlyoutContent.IsHitTestVisible = true;
                Mouse.Synchronize();
            }

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

    private async Task SynchronizeLayoutAfterResizeAsync()
    {
        await Dispatcher.InvokeAsync(() =>
        {
            // Hidden の ScrollViewer は表示上のバーがなくても内部オフセットを保持できる。
            // 伸縮後の見た目と入力座標を一致させるため、先頭へ戻して最終サイズで再配置する。
            FlyoutContent.ScrollToVerticalOffset(0);
            FlyoutContent.InvalidateMeasure();
            FlyoutSurface.InvalidateArrange();
            UpdateLayout();
        }, DispatcherPriority.Loaded);

        await WaitForNextRenderAsync();
        FlyoutContent.IsHitTestVisible = true;
        Mouse.Synchronize();
    }

    private double MeasureDesiredSurfaceHeight()
    {
        double horizontalBorder = FlyoutSurface.BorderThickness.Left + FlyoutSurface.BorderThickness.Right;
        double availableContentWidth = Math.Max(1, FlyoutSurface.ActualWidth - horizontalBorder);

        // ScrollViewer の現在の viewport 高さを計測に使うと、折りたたみ後も以前の高さが
        // DesiredSize に残る。高さ制約を外して、中身そのものが必要とする高さを再計測する。
        FlyoutContentPanel.Measure(new Size(availableContentWidth, double.PositiveInfinity));
        return FlyoutMotionCalculator.CalculateDesiredSurfaceHeight(
            FlyoutContentPanel.DesiredSize.Height,
            0,
            FlyoutSurface.BorderThickness.Top + FlyoutSurface.BorderThickness.Bottom);
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

    private async Task PrepareOpeningSurfaceAsync(CancellationToken cancellationToken)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            FlyoutContent.InvalidateMeasure();
            FlyoutSurface.InvalidateVisual();
            UpdateLayout();
        }, DispatcherPriority.Render, CancellationToken.None);

        if (!cancellationToken.IsCancellationRequested)
        {
            await WaitForNextRenderAsync();
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            await Dispatcher.InvokeAsync(
                static () => { },
                DispatcherPriority.ContextIdle,
                CancellationToken.None);
        }
    }

    private void BeginSurfaceMotionAppearance()
    {
        if (isMotionBackdropSuspended)
        {
            return;
        }

        // Desktop AcrylicはHWND全体へ描かれる。固定HWND内で前景Visualだけを
        // 移動する間は停止し、完成位置に背景だけが残らないようにする。
        FlyoutPositioner.SetBackdropEnabled(this, false);
        isMotionBackdropSuspended = true;
    }

    private void EndSurfaceMotionAppearance()
    {
        if (!isMotionBackdropSuspended)
        {
            return;
        }

        FlyoutPositioner.SetBackdropEnabled(this, true);
        isMotionBackdropSuspended = false;
    }

    private void SetSurfaceOffset(SurfaceOffset offset)
    {
        FlyoutSurfaceTranslation.X = offset.X;
        FlyoutSurfaceTranslation.Y = offset.Y;
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
        SetSurfaceOffset(SurfaceOffset.Zero);
        EndSurfaceMotionAppearance();
        _ = FlyoutPositioner.TrySetCloaked(this, false);
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

        RequestContentResize();
    }

    private void HandleDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (observedViewModel is not null)
        {
            observedViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
            observedViewModel.DisplayOffRequested -= HandleDisplayOffRequested;
        }

        observedViewModel = e.NewValue as MainWindowViewModel;
        if (observedViewModel is not null)
        {
            observedViewModel.PropertyChanged += HandleViewModelPropertyChanged;
            observedViewModel.DisplayOffRequested += HandleDisplayOffRequested;
        }
    }

    private async void HandleDisplayOffRequested(object? sender, EventArgs e)
    {
        if (sender is not MainWindowViewModel viewModel)
        {
            return;
        }

        await HideFlyoutAsync();
        if (!IsVisible)
        {
            viewModel.TurnOffDisplay();
        }
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.IsAcExpanded)
            or nameof(MainWindowViewModel.IsBatteryExpanded)
            or nameof(MainWindowViewModel.HasGeneralError)
            or nameof(MainWindowViewModel.HasDisplayOffError)
            or nameof(MainWindowViewModel.HasAcError)
            or nameof(MainWindowViewModel.HasBatteryError)
            or nameof(MainWindowViewModel.HasSleepError))
        {
            RequestContentResize();
        }
    }

    private void RequestContentResize()
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
        observedViewModel?.CancelDisplayOffCountdown();
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
            RequestContentResize();
        }
    }

    private void ExpandBattery_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Expand(PowerSettingTarget.Battery);
            RequestContentResize();
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
