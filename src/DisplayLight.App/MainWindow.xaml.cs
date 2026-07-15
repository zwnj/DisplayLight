using System.ComponentModel;
using System.Windows;

namespace DisplayLight.App;

public partial class MainWindow : Window
{
    private bool isCloseAllowed;

    public MainWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? ExitRequested;

    public void ShowAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        _ = Activate();
    }

    public void AllowClose() => isCloseAllowed = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!isCloseAllowed)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void ExitButton_Click(object sender, RoutedEventArgs e) =>
        ExitRequested?.Invoke(this, EventArgs.Empty);
}
