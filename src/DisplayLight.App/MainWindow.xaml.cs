using System.ComponentModel;
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
    private bool isPositioning;
    private bool useLightTheme = true;
    private NativeRectangle? lastIconBounds;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += HandleSourceInitialized;
        SizeChanged += HandleSizeChanged;
    }

    public event EventHandler? ExitRequested;

    public event EventHandler? FlyoutHidden;

    internal void ShowAt(NativeRectangle? iconBounds, bool focusPrimaryAction)
    {
        lastIconBounds = iconBounds;
        if (!IsVisible)
        {
            Show();
        }

        PositionFlyout();

        _ = Activate();
        if (focusPrimaryAction)
        {
            _ = SleepToggleButton.Focus();
        }
    }

    private void PositionFlyout()
    {
        if (isPositioning)
        {
            return;
        }

        isPositioning = true;
        try
        {
            UpdateLayout();
            FlyoutPositioner.Position(this, lastIconBounds);
        }
        catch (Win32Exception)
        {
            Rect workArea = SystemParameters.WorkArea;
            Left = Math.Max(workArea.Left + 12, workArea.Right - ActualWidth - 12);
            Top = Math.Max(workArea.Top + 12, workArea.Bottom - ActualHeight - 12);
        }
        finally
        {
            isPositioning = false;
        }
    }

    internal void ToggleAt(NativeRectangle? iconBounds, bool focusPrimaryAction)
    {
        if (IsVisible)
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

        base.OnClosing(e);
    }

    private void HideFlyout()
    {
        if (!IsVisible)
        {
            return;
        }

        Hide();
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
