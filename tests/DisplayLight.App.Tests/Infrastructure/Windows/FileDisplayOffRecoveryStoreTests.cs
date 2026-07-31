using System.IO;
using DisplayLight.App.Infrastructure.Windows;
using DisplayLight.Core.Power;

namespace DisplayLight.App.Tests.Infrastructure.Windows;

public sealed class FileDisplayOffRecoveryStoreTests : IDisposable
{
    private readonly string directoryPath = Path.Combine(
        Path.GetTempPath(),
        "DisplayLight.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveLoadAndDeleteRoundTripsRecoveryMetadata()
    {
        string filePath = Path.Combine(directoryPath, "display-off-recovery.json");
        FileDisplayOffRecoveryStore store = new(filePath);
        DisplayOffRecoveryState expected = new(
            Guid.NewGuid(),
            PowerSettingTarget.Battery,
            OriginalSeconds: 1800,
            TemporarySeconds: 1,
            CreatedAt: DateTimeOffset.UtcNow);

        store.Save(expected);
        DisplayOffRecoveryState? actual = store.Load();
        store.Delete();

        Assert.Equal(expected, actual);
        Assert.False(File.Exists(filePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
