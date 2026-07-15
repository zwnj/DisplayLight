using System.IO;
using System.Text.Json;
using DisplayLight.Core.Abstractions;
using DisplayLight.Core.Settings;

namespace DisplayLight.App.Infrastructure.Settings;

internal sealed class JsonUserSettingsStore : IUserSettingsStore
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string directoryPath;
    private readonly string settingsPath;

    public JsonUserSettingsStore(string? directoryPath = null)
    {
        this.directoryPath = directoryPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DisplayLight");
        settingsPath = Path.Combine(this.directoryPath, SettingsFileName);
    }

    public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsPath))
        {
            return new UserSettings();
        }

        try
        {
            await using FileStream stream = new(
                settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            UserSettings? settings = await JsonSerializer.DeserializeAsync<UserSettings>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (settings is null || settings.SchemaVersion > UserSettings.CurrentSchemaVersion)
            {
                throw new JsonException("Unsupported or empty settings file.");
            }

            return settings.Normalize();
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            PreserveUnreadableSettings();
            return new UserSettings();
        }
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(directoryPath);
        string temporaryPath = Path.Combine(directoryPath, $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings.Normalize(),
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (IOException)
        {
            // Cleanup must not hide the original save failure.
        }
        catch (UnauthorizedAccessException)
        {
            // A stale uniquely named temp file is safer than masking the original failure.
        }
    }

    private void PreserveUnreadableSettings()
    {
        string backupPath = Path.Combine(
            directoryPath,
            $"settings.corrupt.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json");

        try
        {
            File.Move(settingsPath, backupPath);
        }
        catch (IOException)
        {
            // Keep the original file in place when it cannot be moved.
        }
        catch (UnauthorizedAccessException)
        {
            // Loading still falls back to defaults when the backup cannot be created.
        }
    }
}
