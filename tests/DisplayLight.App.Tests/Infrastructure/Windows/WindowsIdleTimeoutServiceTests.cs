using DisplayLight.App.Infrastructure.Windows;
using DisplayLight.Core.Power;

namespace DisplayLight.App.Tests.Infrastructure.Windows;

public sealed class WindowsIdleTimeoutServiceTests
{
    private static readonly Guid SchemeGuid = new("d6e6a9cb-109f-40f8-922f-6d5602c301c9");

    [Fact]
    public async Task ReadAsyncReadsDisplayAndSleepValuesFromOneActiveScheme()
    {
        FakePowerSchemeNativeApi nativeApi = new()
        {
            AcDisplaySeconds = 600,
            AcSleepSeconds = 1500,
            DcDisplaySeconds = 300,
            DcSleepSeconds = 600,
        };
        WindowsIdleTimeoutService service = new(nativeApi);

        IdleTimeoutValues values = await service.ReadAsync(CancellationToken.None);

        Assert.Equal(new IdleTimeoutValues(600, 1500, 300, 600), values);
        Assert.Equal(
            ["Get", "ReadAcDisplay", "ReadAcSleep", "ReadDcDisplay", "ReadDcSleep"],
            nativeApi.Calls);
    }

    [Fact]
    public async Task SetAsyncWritesPairedAcValuesActivatesAndVerifies()
    {
        FakePowerSchemeNativeApi nativeApi = CreateNativeApi();
        WindowsIdleTimeoutService service = new(nativeApi);

        IdleTimeoutValues values = await service.SetAsync(
            PowerSettingTarget.AcPower,
            new IdleTimeoutSelection(
                DisplayTimeoutPreset.ThirtyMinutes,
                SleepDelayPreset.FifteenMinutes),
            CancellationToken.None);

        Assert.Equal(1800u, values.AcDisplaySeconds);
        Assert.Equal(2700u, values.AcSleepSeconds);
        Assert.Equal(300u, values.BatteryDisplaySeconds);
        Assert.Equal(600u, values.BatterySleepSeconds);
        Assert.Contains("WriteAcDisplay:1800", nativeApi.Calls);
        Assert.Contains("WriteAcSleep:2700", nativeApi.Calls);
        Assert.DoesNotContain(
            nativeApi.Calls,
            call => call.StartsWith("WriteDc", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetAsyncWritesZeroSleepTimeoutForNever()
    {
        FakePowerSchemeNativeApi nativeApi = CreateNativeApi();
        WindowsIdleTimeoutService service = new(nativeApi);

        IdleTimeoutValues values = await service.SetAsync(
            PowerSettingTarget.Battery,
            new IdleTimeoutSelection(
                DisplayTimeoutPreset.FiveMinutes,
                SleepDelayPreset.Never),
            CancellationToken.None);

        Assert.Equal(300u, values.BatteryDisplaySeconds);
        Assert.Equal(0u, values.BatterySleepSeconds);
        Assert.Contains("WriteDcSleep:0", nativeApi.Calls);
    }

    [Fact]
    public async Task SetAsyncRestoresPreviousPairWhenReadBackDoesNotMatch()
    {
        FakePowerSchemeNativeApi nativeApi = CreateNativeApi();
        WindowsIdleTimeoutService service = new(nativeApi);
        nativeApi.IgnoreRequestedAcSleepValue = 2700;

        IdleTimeoutApplyException exception = await Assert.ThrowsAsync<IdleTimeoutApplyException>(
            () => service.SetAsync(
                PowerSettingTarget.AcPower,
                new IdleTimeoutSelection(
                    DisplayTimeoutPreset.ThirtyMinutes,
                    SleepDelayPreset.FifteenMinutes),
                CancellationToken.None));

        Assert.True(exception.RestoreSucceeded);
        Assert.Equal(new IdleTimeoutPair(600, 1500), exception.ActualValues?.For(PowerSettingTarget.AcPower));
        Assert.Equal(600u, nativeApi.AcDisplaySeconds);
        Assert.Equal(1500u, nativeApi.AcSleepSeconds);
        Assert.Contains("復旧しました", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetAsyncRestoresDisplayWhenSleepWriteFails()
    {
        FakePowerSchemeNativeApi nativeApi = CreateNativeApi();
        WindowsIdleTimeoutService service = new(nativeApi);
        nativeApi.FailNextAcSleepWrite = true;

        IdleTimeoutApplyException exception = await Assert.ThrowsAsync<IdleTimeoutApplyException>(
            () => service.SetAsync(
                PowerSettingTarget.AcPower,
                new IdleTimeoutSelection(
                    DisplayTimeoutPreset.ThirtyMinutes,
                    SleepDelayPreset.FifteenMinutes),
                CancellationToken.None));

        Assert.True(exception.RestoreSucceeded);
        Assert.Equal(600u, nativeApi.AcDisplaySeconds);
        Assert.Equal(1500u, nativeApi.AcSleepSeconds);
    }

    private static FakePowerSchemeNativeApi CreateNativeApi() => new()
    {
        AcDisplaySeconds = 600,
        AcSleepSeconds = 1500,
        DcDisplaySeconds = 300,
        DcSleepSeconds = 600,
    };

    private sealed class FakePowerSchemeNativeApi : IPowerSchemeNativeApi
    {
        public List<string> Calls { get; } = [];

        public uint AcDisplaySeconds { get; set; }

        public uint AcSleepSeconds { get; set; }

        public uint DcDisplaySeconds { get; set; }

        public uint DcSleepSeconds { get; set; }

        public uint? IgnoreRequestedAcSleepValue { get; set; }

        public bool FailNextAcSleepWrite { get; set; }

        public Guid GetActiveScheme()
        {
            Calls.Add("Get");
            return SchemeGuid;
        }

        public uint ReadAcDisplayValue(Guid schemeGuid)
        {
            AssertScheme(schemeGuid);
            Calls.Add("ReadAcDisplay");
            return AcDisplaySeconds;
        }

        public uint ReadDcDisplayValue(Guid schemeGuid)
        {
            AssertScheme(schemeGuid);
            Calls.Add("ReadDcDisplay");
            return DcDisplaySeconds;
        }

        public uint ReadAcSleepValue(Guid schemeGuid)
        {
            AssertScheme(schemeGuid);
            Calls.Add("ReadAcSleep");
            return AcSleepSeconds;
        }

        public uint ReadDcSleepValue(Guid schemeGuid)
        {
            AssertScheme(schemeGuid);
            Calls.Add("ReadDcSleep");
            return DcSleepSeconds;
        }

        public void WriteAcDisplayValue(Guid schemeGuid, uint seconds)
        {
            AssertScheme(schemeGuid);
            Calls.Add($"WriteAcDisplay:{seconds}");
            AcDisplaySeconds = seconds;
        }

        public void WriteDcDisplayValue(Guid schemeGuid, uint seconds)
        {
            AssertScheme(schemeGuid);
            Calls.Add($"WriteDcDisplay:{seconds}");
            DcDisplaySeconds = seconds;
        }

        public void WriteAcSleepValue(Guid schemeGuid, uint seconds)
        {
            AssertScheme(schemeGuid);
            Calls.Add($"WriteAcSleep:{seconds}");
            if (FailNextAcSleepWrite)
            {
                FailNextAcSleepWrite = false;
                throw new InvalidOperationException("Sleep write failed.");
            }

            if (IgnoreRequestedAcSleepValue != seconds)
            {
                AcSleepSeconds = seconds;
            }
        }

        public void WriteDcSleepValue(Guid schemeGuid, uint seconds)
        {
            AssertScheme(schemeGuid);
            Calls.Add($"WriteDcSleep:{seconds}");
            DcSleepSeconds = seconds;
        }

        public void Activate(Guid schemeGuid)
        {
            AssertScheme(schemeGuid);
            Calls.Add("Activate");
        }

        private static void AssertScheme(Guid schemeGuid) => Assert.Equal(SchemeGuid, schemeGuid);
    }
}
