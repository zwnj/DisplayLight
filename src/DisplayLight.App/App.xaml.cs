using System.Windows;
using DisplayLight.App.Infrastructure.Settings;
using DisplayLight.App.Infrastructure.SingleInstance;
using DisplayLight.App.Infrastructure.Tray;
using DisplayLight.App.Infrastructure.Windows;
using DisplayLight.App.Presentation;
using DisplayLight.Core.Abstractions;

namespace DisplayLight.App;

public partial class App : Application, IDisposable
{
    private SingleInstanceCoordinator? singleInstanceCoordinator;
    private WindowsSleepPreventionService? sleepPreventionService;
    private MainWindowViewModel? viewModel;
    private MainWindow? mainWindow;
    private TrayIconService? trayIconService;
    private bool isShuttingDown;
    private bool isCleanedUp;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            singleInstanceCoordinator = new SingleInstanceCoordinator(Dispatcher, ShowMainWindow);
            if (!singleInstanceCoordinator.IsPrimaryInstance)
            {
                singleInstanceCoordinator.NotifyPrimaryInstance();
                singleInstanceCoordinator.Dispose();
                singleInstanceCoordinator = null;
                Shutdown();
                return;
            }

            IPowerSchemeNativeApi powerSchemeApi = new PowerSchemeNativeApi();
            IDisplayTimeoutService displayTimeoutService = new WindowsDisplayTimeoutService(powerSchemeApi);
            sleepPreventionService = new WindowsSleepPreventionService();
            IPowerSourceProvider powerSourceProvider = new WindowsPowerSourceProvider();
            IUserSettingsStore settingsStore = new JsonUserSettingsStore();

            viewModel = new MainWindowViewModel(
                displayTimeoutService,
                sleepPreventionService,
                powerSourceProvider,
                settingsStore);
            mainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            MainWindow = mainWindow;
            mainWindow.ExitRequested += HandleExitRequested;
            mainWindow.Show();

            trayIconService = new TrayIconService(viewModel, ShowMainWindow, RequestShutdown);
            trayIconService.Initialize(mainWindow);
            await viewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"DisplayLightを起動できませんでした。\n\n{exception.Message}",
                "DisplayLight",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            RequestShutdown(exitCode: 1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void ShowMainWindow()
    {
        if (mainWindow is null || isShuttingDown)
        {
            return;
        }

        mainWindow.ShowAndActivate();
    }

    private void HandleExitRequested(object? sender, EventArgs e) => RequestShutdown();

    private void RequestShutdown() => RequestShutdown(exitCode: 0);

    private void RequestShutdown(int exitCode)
    {
        if (isShuttingDown)
        {
            return;
        }

        isShuttingDown = true;
        Cleanup();
        Shutdown(exitCode);
    }

    private void Cleanup()
    {
        if (isCleanedUp)
        {
            return;
        }

        isCleanedUp = true;

        TryCleanup(() => trayIconService?.Dispose());
        trayIconService = null;

        TryCleanup(() => viewModel?.Dispose());
        viewModel = null;

        TryCleanup(() => sleepPreventionService?.Dispose());
        sleepPreventionService = null;

        if (mainWindow is not null)
        {
            mainWindow.ExitRequested -= HandleExitRequested;
            mainWindow.AllowClose();
            TryCleanup(mainWindow.Close);
            mainWindow = null;
            MainWindow = null;
        }

        TryCleanup(() => singleInstanceCoordinator?.Dispose());
        singleInstanceCoordinator = null;
    }

    private static void TryCleanup(Action? cleanup)
    {
        try
        {
            cleanup?.Invoke();
        }
        catch
        {
            // Continue releasing the remaining native resources during shutdown.
        }
    }
}
