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

    private static readonly string[] ExpectedSleepDelayNames =
    [
        "画面オフと同時",
        "画面オフの5分後",
        "画面オフの15分後",
        "画面オフの30分後",
        "画面オフの60分後",
        "PCをスリープしない",
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
                Assert.True(window.DisplayOffButton.MinHeight >= 44);
                Assert.Equal(44, window.AcActionRow.Height);
                Assert.Equal(44, window.BatteryActionRow.Height);
                Assert.Equal(new CornerRadius(8), window.FlyoutSurface.CornerRadius);
                Assert.Equal(ScrollBarVisibility.Hidden, window.FlyoutContent.VerticalScrollBarVisibility);
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
                Assert.Equal("AC電源時の画面オフ時間", AutomationProperties.GetName(window.AcPresetList));
                Assert.Equal("バッテリー時の画面オフ時間", AutomationProperties.GetName(window.BatteryPresetList));
                Assert.Equal(
                    "AC電源時、画面オフからPCスリープまでの時間",
                    AutomationProperties.GetName(window.AcSleepPresetList));
                Assert.Equal(
                    "バッテリー時、画面オフからPCスリープまでの時間",
                    AutomationProperties.GetName(window.BatterySleepPresetList));
                Assert.IsType<DiscretePresetListBox>(window.AcPresetList);
                Assert.Equal(6, window.AcPresetList.AlternationCount);
                Assert.Equal(ExpectedPresetNames, GetAutomationNames(window.AcPresetList));
                Assert.Equal(ExpectedPresetNames, GetAutomationNames(window.BatteryPresetList));
                Assert.Equal(ExpectedSleepDelayNames, GetAutomationNames(window.AcSleepPresetList));
                Assert.Equal(ExpectedSleepDelayNames, GetAutomationNames(window.BatterySleepPresetList));
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
