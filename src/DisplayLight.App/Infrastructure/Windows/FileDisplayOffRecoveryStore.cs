using System.IO;
using System.Text.Json;

namespace DisplayLight.App.Infrastructure.Windows;

internal sealed class FileDisplayOffRecoveryStore : IDisplayOffRecoveryStore
{
    private const string RecoveryFileName = "display-off-recovery.json";
    private readonly string filePath;

    public FileDisplayOffRecoveryStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DisplayLight",
            RecoveryFileName))
    {
    }

    internal FileDisplayOffRecoveryStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    public DisplayOffRecoveryState? Load()
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<DisplayOffRecoveryState>(json)
                ?? throw new InvalidDataException("手動消灯の復旧情報が空です。");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidDataException(
                "手動消灯の復旧情報を読み取れませんでした。Windowsのディスプレイ消灯設定を確認してください。",
                exception);
        }
    }

    public void Save(DisplayOffRecoveryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        string? directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("手動消灯の復旧情報を保存する場所を取得できませんでした。");
        }

        Directory.CreateDirectory(directoryPath);
        string temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state));
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public void Delete()
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
