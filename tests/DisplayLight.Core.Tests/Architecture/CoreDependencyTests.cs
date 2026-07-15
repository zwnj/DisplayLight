using System.Reflection;

namespace DisplayLight.Core.Tests.Architecture;

public sealed class CoreDependencyTests
{
    private static readonly string[] WindowsDesktopAssemblyPrefixes =
    [
        "PresentationCore",
        "PresentationFramework",
        "ReachFramework",
        "System.Xaml",
        "UIAutomation",
        "WindowsBase",
    ];

    [Fact]
    public void CoreAssemblyDoesNotReferenceWindowsDesktopAssemblies()
    {
        Assembly coreAssembly = typeof(CoreAssemblyMarker).Assembly;
        string[] referenceNames = coreAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            referenceNames,
            referenceName => WindowsDesktopAssemblyPrefixes.Any(
                prefix => referenceName.StartsWith(prefix, StringComparison.Ordinal)));
    }
}
