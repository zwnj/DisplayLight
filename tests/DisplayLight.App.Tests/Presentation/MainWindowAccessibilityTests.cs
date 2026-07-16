using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DisplayLight.App.Presentation.Controls;

namespace DisplayLight.App.Tests.Presentation;

public sealed class MainWindowAccessibilityTests
{
    private static readonly string[] ExpectedPresetNames =
    [
        "1分",
        "5分",
        "10分",
        "30分",
        "60分",
        "無期限",
    ];

    [Fact]
    public void MainWindowUsesFlyoutWindowAttributes()
    {
        RunInSta(() =>
        {
            MainWindow window = new();
            try
            {
                Assert.Equal(372, window.Width);
                Assert.Equal(SizeToContent.Height, window.SizeToContent);
                Assert.Equal(WindowStyle.None, window.WindowStyle);
                Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
                Assert.False(window.ShowInTaskbar);
                Assert.IsType<ToggleButton>(window.SleepToggleButton);
                Assert.True(window.AcPowerOnlyCheckBox.MinHeight >= 44);
                Assert.True(window.AcApplyButton.MinHeight >= 44);
                Assert.True(window.BatteryApplyButton.MinHeight >= 44);
                Assert.Equal(44, window.AcActionRow.Height);
                Assert.Equal(44, window.BatteryActionRow.Height);
            }
            finally
            {
                window.AllowClose();
                window.Close();
            }
        });
    }

    [Fact]
    public void PresetSelectorsExposeTargetAndActualPresetNames()
    {
        RunInSta(() =>
        {
            MainWindow window = new();
            try
            {
                Assert.Equal("AC電源時の消灯時間", AutomationProperties.GetName(window.AcPresetList));
                Assert.Equal("バッテリー時の消灯時間", AutomationProperties.GetName(window.BatteryPresetList));
                Assert.IsType<DiscretePresetListBox>(window.AcPresetList);
                Assert.Equal(6, window.AcPresetList.AlternationCount);
                Assert.Equal(ExpectedPresetNames, GetAutomationNames(window.AcPresetList));
                Assert.Equal(ExpectedPresetNames, GetAutomationNames(window.BatteryPresetList));
            }
            finally
            {
                window.AllowClose();
                window.Close();
            }
        });
    }

    private static string[] GetAutomationNames(ListBox listBox) =>
        listBox.Items
            .Cast<ListBoxItem>()
            .Select(AutomationProperties.GetName)
            .ToArray();

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
