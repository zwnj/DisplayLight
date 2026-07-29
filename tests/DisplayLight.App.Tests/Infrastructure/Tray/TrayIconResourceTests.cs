using DisplayLight.App.Infrastructure.Tray;

namespace DisplayLight.App.Tests.Infrastructure.Tray;

public sealed class TrayIconResourceTests
{
    [Theory]
    [InlineData("DisplayLight.Normal.ico")]
    [InlineData("DisplayLight.Active.ico")]
    [InlineData("DisplayLight.Waiting.ico")]
    [InlineData("DisplayLight.Error.ico")]
    public void OpenReturnsEmbeddedIcon(string fileName)
    {
        using Stream stream = TrayIconResource.Open(fileName);

        Assert.True(stream.Length > 0);
    }
}
