using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace DisplayLight.App.Presentation;

internal sealed class ApplicationThemeManager : IDisposable
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private readonly ResourceDictionary resources;
    private bool isDisposed;

    internal event EventHandler? ThemeChanged;

    internal bool UseLightTheme { get; private set; } = true;

    internal ApplicationThemeManager(ResourceDictionary resources)
    {
        this.resources = resources;
        SystemParameters.StaticPropertyChanged += HandleSystemParametersChanged;
        Apply();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        SystemParameters.StaticPropertyChanged -= HandleSystemParametersChanged;
    }

    private void Apply()
    {
        UseLightTheme = ReadUseLightTheme();
        ApplyPalette(resources, SystemParameters.HighContrast, UseLightTheme);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    internal static void ApplyPalette(ResourceDictionary resources, bool highContrast, bool useLightTheme)
    {
        ArgumentNullException.ThrowIfNull(resources);

        if (highContrast)
        {
            resources["SurfaceBrush"] = SystemColors.WindowBrush;
            resources["SurfaceElevatedBrush"] = SystemColors.ControlBrush;
            resources["TextPrimaryBrush"] = SystemColors.WindowTextBrush;
            resources["TextSecondaryBrush"] = SystemColors.GrayTextBrush;
            resources["BorderBrush"] = SystemColors.ActiveBorderBrush;
            resources["AccentBrush"] = SystemColors.HighlightBrush;
            resources["AccentTextBrush"] = SystemColors.HighlightTextBrush;
            resources["HoverBrush"] = SystemColors.ControlLightBrush;
            resources["ErrorBrush"] = new SolidColorBrush(SystemColors.WindowTextColor);
            resources["SuccessBrush"] = SystemColors.WindowTextBrush;
            return;
        }

        resources["SurfaceBrush"] = BrushFrom(useLightTheme ? "#EAF7F7FA" : "#E61C1C1E");
        resources["SurfaceElevatedBrush"] = BrushFrom(useLightTheme ? "#CCFFFFFF" : "#B83A3A3E");
        resources["TextPrimaryBrush"] = BrushFrom(useLightTheme ? "#1B1B1F" : "#F5F5F5");
        resources["TextSecondaryBrush"] = BrushFrom(useLightTheme ? "#60606A" : "#B8B8C0");
        resources["BorderBrush"] = BrushFrom(useLightTheme ? "#668A8F99" : "#665C5C63");
        resources["AccentBrush"] = BrushFrom(useLightTheme ? "#2563EB" : "#6EA8FE");
        resources["AccentTextBrush"] = BrushFrom(useLightTheme ? "#FFFFFF" : "#0F172A");
        resources["HoverBrush"] = BrushFrom(useLightTheme ? "#99FFFFFF" : "#80505056");
        resources["ErrorBrush"] = BrushFrom(useLightTheme ? "#B42318" : "#FFB4AB");
        resources["SuccessBrush"] = BrushFrom(useLightTheme ? "#107C41" : "#6CCB8E");
    }

    private static bool ReadUseLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch
        {
            return true;
        }
    }

    private static SolidColorBrush BrushFrom(string value)
    {
        SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }

    private void HandleSystemParametersChanged(object? sender, PropertyChangedEventArgs e) => Apply();

}
