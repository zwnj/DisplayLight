using System.IO;
using DisplayLight.App.Infrastructure.Settings;
using DisplayLight.Core.Power;
using DisplayLight.Core.Settings;

namespace DisplayLight.App.Tests.Infrastructure.Settings;

public sealed class JsonUserSettingsStoreTests
{
    [Fact]
    public async Task LoadAsyncReturnsDefaultsWhenFileDoesNotExist()
    {
        using TestDirectory directory = new();
        JsonUserSettingsStore store = new(directory.Path);

        UserSettings settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(new UserSettings(), settings);
    }

    [Fact]
    public async Task SaveAsyncReplacesExistingFileAndLoadsNormalizedSettings()
    {
        using TestDirectory directory = new();
        JsonUserSettingsStore store = new(directory.Path);
        UserSettings first = new()
        {
            SelectedAcTimeout = DisplayTimeoutPreset.OneMinute,
        };
        UserSettings second = new()
        {
            SelectedAcTimeout = DisplayTimeoutPreset.SixtyMinutes,
            SelectedBatteryTimeout = DisplayTimeoutPreset.Never,
            IsAcPowerOnly = true,
        };

        await store.SaveAsync(first, CancellationToken.None);
        await store.SaveAsync(second, CancellationToken.None);
        UserSettings loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(second, loaded);
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task LoadAsyncPreservesUnreadableFileAndReturnsDefaults()
    {
        using TestDirectory directory = new();
        Directory.CreateDirectory(directory.Path);
        await File.WriteAllTextAsync(
            Path.Combine(directory.Path, "settings.json"),
            "{ invalid json",
            CancellationToken.None);
        JsonUserSettingsStore store = new(directory.Path);

        UserSettings loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(new UserSettings(), loaded);
        Assert.False(File.Exists(Path.Combine(directory.Path, "settings.json")));
        Assert.Single(Directory.EnumerateFiles(directory.Path, "settings.corrupt.*.json"));
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DisplayLight.Tests",
                Guid.NewGuid().ToString("N"));
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
