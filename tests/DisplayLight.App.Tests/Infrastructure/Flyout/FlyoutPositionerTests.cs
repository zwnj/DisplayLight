using System.Windows.Media;
using DisplayLight.App.Infrastructure.Flyout;

namespace DisplayLight.App.Tests.Infrastructure.Flyout;

public sealed class FlyoutPositionerTests
{
    [Fact]
    public void ResizeBackgroundUsesOpaqueSurfaceColor()
    {
        Color result = FlyoutPositioner.GetOpaqueSurfaceColor(
            new SolidColorBrush(Color.FromArgb(0xE6, 0x1C, 0x1C, 0x1E)));

        Assert.Equal(Color.FromRgb(0x1C, 0x1C, 0x1E), result);
    }
}
