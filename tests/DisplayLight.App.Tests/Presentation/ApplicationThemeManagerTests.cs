using System.Windows;
using System.Windows.Media;
using DisplayLight.App.Presentation;

namespace DisplayLight.App.Tests.Presentation;

public sealed class ApplicationThemeManagerTests
{
    [Theory]
    [InlineData(true, 0xEAF7F7FAu, 0xFF1B1B1Fu, 0xFF2563EBu)]
    [InlineData(false, 0xE61C1C1Eu, 0xFFF5F5F5u, 0xFF6EA8FEu)]
    public void ApplyPaletteUsesExpectedThemeColors(
        bool useLightTheme,
        uint expectedSurface,
        uint expectedText,
        uint expectedAccent)
    {
        ResourceDictionary resources = new();

        ApplicationThemeManager.ApplyPalette(resources, highContrast: false, useLightTheme);

        Assert.Equal(expectedSurface, GetArgb(resources, "SurfaceBrush"));
        Assert.Equal(expectedText, GetArgb(resources, "TextPrimaryBrush"));
        Assert.Equal(expectedAccent, GetArgb(resources, "AccentBrush"));
    }

    [Fact]
    public void ApplyPaletteUsesSystemBrushesForHighContrast()
    {
        ResourceDictionary resources = new();

        ApplicationThemeManager.ApplyPalette(resources, highContrast: true, useLightTheme: false);

        Assert.Same(SystemColors.WindowBrush, resources["SurfaceBrush"]);
        Assert.Same(SystemColors.WindowTextBrush, resources["TextPrimaryBrush"]);
        Assert.Same(SystemColors.HighlightBrush, resources["AccentBrush"]);
        Assert.Same(SystemColors.HighlightTextBrush, resources["AccentTextBrush"]);
    }

    private static uint GetArgb(ResourceDictionary resources, string key)
    {
        Color color = Assert.IsType<SolidColorBrush>(resources[key]).Color;
        return ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
    }
}
