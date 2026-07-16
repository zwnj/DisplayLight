using System.Globalization;
using System.Windows;
using DisplayLight.App.Presentation.Controls;

namespace DisplayLight.App.Tests.Presentation;

public sealed class CurrentPresetMarkerVisibilityConverterTests
{
    private readonly CurrentPresetMarkerVisibilityConverter converter = new();

    [Fact]
    public void ConvertShowsCurrentPositionOnlyWhileAChangeIsPending()
    {
        object visible = converter.Convert(
            [2, 2, true],
            typeof(Visibility),
            parameter: null!,
            CultureInfo.InvariantCulture);
        object notPending = converter.Convert(
            [2, 2, false],
            typeof(Visibility),
            parameter: null!,
            CultureInfo.InvariantCulture);
        object otherPosition = converter.Convert(
            [3, 2, true],
            typeof(Visibility),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal(Visibility.Visible, visible);
        Assert.Equal(Visibility.Collapsed, notPending);
        Assert.Equal(Visibility.Collapsed, otherPosition);
    }
}
