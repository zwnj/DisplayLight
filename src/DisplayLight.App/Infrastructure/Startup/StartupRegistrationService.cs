using System.IO;
using System.Security;
using Velopack.Locators;

namespace DisplayLight.App.Infrastructure.Startup;

internal sealed class StartupRegistrationService(
    IStartupRegistrationStore store,
    string? launcherPath)
{
    internal static StartupRegistrationService CreateForCurrentInstallation()
    {
        IVelopackLocator locator = VelopackLocator.Current;
        string? currentLauncherPath = null;
        if (!locator.IsPortable &&
            locator.CurrentlyInstalledVersion is not null &&
            !string.IsNullOrWhiteSpace(locator.RootAppDir) &&
            !string.IsNullOrWhiteSpace(locator.ThisExeRelativePath))
        {
            currentLauncherPath = Path.GetFullPath(Path.Combine(
                locator.RootAppDir,
                locator.ThisExeRelativePath));
        }

        return new StartupRegistrationService(
            new WindowsStartupRegistrationStore(),
            currentLauncherPath);
    }

    internal bool TryEnsureRegistered()
    {
        string? currentLauncherPath = launcherPath;
        if (string.IsNullOrWhiteSpace(currentLauncherPath) || !File.Exists(currentLauncherPath))
        {
            return false;
        }

        try
        {
            string expectedCommand = CreateCommand(currentLauncherPath);
            string? currentCommand = store.ReadCommand();
            if (string.Equals(currentCommand, expectedCommand, StringComparison.Ordinal))
            {
                // Task Managerの無効状態は別のWindows管理領域にあるため、同じ値を書き直さない。
                return true;
            }

            store.WriteCommand(expectedCommand);
            return true;
        }
        catch (Exception exception) when (IsRegistrationFailure(exception))
        {
            return false;
        }
    }

    internal bool TryRemoveRegistration()
    {
        try
        {
            store.DeleteCommand();
            return true;
        }
        catch (Exception exception) when (IsRegistrationFailure(exception))
        {
            return false;
        }
    }

    internal static string CreateCommand(string path) =>
        $"\"{path}\" {ApplicationLaunchModeCalculator.StartupArgument}";

    private static bool IsRegistrationFailure(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException or IOException;
}
