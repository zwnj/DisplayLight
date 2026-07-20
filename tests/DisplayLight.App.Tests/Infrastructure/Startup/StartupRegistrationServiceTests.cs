using DisplayLight.App.Infrastructure.Startup;

namespace DisplayLight.App.Tests.Infrastructure.Startup;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public void RegistersInstalledLauncherWithHiddenStartupArgument()
    {
        FakeStartupRegistrationStore store = new();
        string launcherPath = Environment.ProcessPath!;
        StartupRegistrationService service = new(store, launcherPath);

        bool succeeded = service.TryEnsureRegistered();

        Assert.True(succeeded);
        Assert.Equal($"\"{launcherPath}\" --startup", store.Command);
        Assert.Equal(1, store.WriteCount);
    }

    [Fact]
    public void DoesNotRewriteExistingCommandSoTaskManagerChoiceIsPreserved()
    {
        string launcherPath = Environment.ProcessPath!;
        string expectedCommand = StartupRegistrationService.CreateCommand(launcherPath);
        FakeStartupRegistrationStore store = new() { Command = expectedCommand };
        StartupRegistrationService service = new(store, launcherPath);

        bool succeeded = service.TryEnsureRegistered();

        Assert.True(succeeded);
        Assert.Equal(0, store.WriteCount);
    }

    [Fact]
    public void PortableLaunchDoesNotCreateStartupEntry()
    {
        FakeStartupRegistrationStore store = new();
        StartupRegistrationService service = new(store, launcherPath: null);

        bool succeeded = service.TryEnsureRegistered();

        Assert.False(succeeded);
        Assert.Null(store.Command);
        Assert.Equal(0, store.WriteCount);
    }

    [Fact]
    public void RemovesRegistrationDuringUninstall()
    {
        FakeStartupRegistrationStore store = new() { Command = "existing" };
        StartupRegistrationService service = new(store, Environment.ProcessPath);

        bool succeeded = service.TryRemoveRegistration();

        Assert.True(succeeded);
        Assert.Null(store.Command);
        Assert.Equal(1, store.DeleteCount);
    }

    private sealed class FakeStartupRegistrationStore : IStartupRegistrationStore
    {
        public string? Command { get; set; }

        public int WriteCount { get; private set; }

        public int DeleteCount { get; private set; }

        public string? ReadCommand() => Command;

        public void WriteCommand(string command)
        {
            Command = command;
            WriteCount++;
        }

        public void DeleteCommand()
        {
            Command = null;
            DeleteCount++;
        }
    }
}
