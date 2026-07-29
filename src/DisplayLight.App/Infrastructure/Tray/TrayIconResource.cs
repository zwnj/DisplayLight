using System.IO;

namespace DisplayLight.App.Infrastructure.Tray;

internal static class TrayIconResource
{
    private const string ResourcePrefix = "DisplayLight.App.Assets.Icons.";

    public static Stream Open(string fileName) =>
        typeof(TrayIconResource).Assembly.GetManifestResourceStream(ResourcePrefix + fileName)
        ?? throw new InvalidOperationException(
            $"通知領域アイコンがアプリに含まれていません：{fileName}");
}
