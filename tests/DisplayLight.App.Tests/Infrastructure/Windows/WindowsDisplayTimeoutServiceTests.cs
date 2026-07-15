using DisplayLight.App.Infrastructure.Windows;
using DisplayLight.Core.Power;

namespace DisplayLight.App.Tests.Infrastructure.Windows;

public sealed class WindowsDisplayTimeoutServiceTests
{
    private static readonly Guid SchemeGuid = new("d6e6a9cb-109f-40f8-922f-6d5602c301c9");

    [Fact]
    public async Task ReadAsyncReadsBothValuesFromOneActiveScheme()
    {
        FakePowerSchemeNativeApi nativeApi = new()
        {
            AcSeconds = 600,
            DcSeconds = 300,
        };
        WindowsDisplayTimeoutService service = new(nativeApi);

        DisplayTimeoutValues values = await service.ReadAsync(CancellationToken.None);

        Assert.Equal(new DisplayTimeoutValues(600, 300), values);
        Assert.Equal(["Get", "ReadAc", "ReadDc"], nativeApi.Calls);
    }

    [Fact]
    public async Task SetAsyncWritesAcValueActivatesSchemeAndVerifiesResult()
    {
        FakePowerSchemeNativeApi nativeApi = new()
        {
            AcSeconds = 600,
            DcSeconds = 300,
        };
        WindowsDisplayTimeoutService service = new(nativeApi);

        DisplayTimeoutValues values = await service.SetAsync(
            PowerSettingTarget.AcPower,
            DisplayTimeoutPreset.ThirtyMinutes,
            CancellationToken.None);

        Assert.Equal(1800u, values.AcSeconds);
        Assert.Equal(300u, values.BatterySeconds);
        Assert.Equal(["Get", "WriteAc:1800", "Activate", "Get", "ReadAc", "ReadDc"], nativeApi.Calls);
    }

    [Fact]
    public async Task SetAsyncWritesZeroForNeverOnBattery()
    {
        FakePowerSchemeNativeApi nativeApi = new()
        {
            AcSeconds = 600,
            DcSeconds = 300,
        };
        WindowsDisplayTimeoutService service = new(nativeApi);

        DisplayTimeoutValues values = await service.SetAsync(
            PowerSettingTarget.Battery,
            DisplayTimeoutPreset.Never,
            CancellationToken.None);

        Assert.Equal(600u, values.AcSeconds);
        Assert.Equal(0u, values.BatterySeconds);
        Assert.Contains("WriteDc:0", nativeApi.Calls);
        Assert.DoesNotContain(nativeApi.Calls, call => call.StartsWith("WriteAc", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetAsyncThrowsWhenReadBackDoesNotMatch()
    {
        FakePowerSchemeNativeApi nativeApi = new()
        {
            AcSeconds = 600,
            DcSeconds = 300,
            IgnoreWrites = true,
        };
        WindowsDisplayTimeoutService service = new(nativeApi);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetAsync(
                PowerSettingTarget.AcPower,
                DisplayTimeoutPreset.ThirtyMinutes,
                CancellationToken.None));

        Assert.Contains("要求値", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FakePowerSchemeNativeApi : IPowerSchemeNativeApi
    {
        public List<string> Calls { get; } = [];

        public uint AcSeconds { get; set; }

        public uint DcSeconds { get; set; }

        public bool IgnoreWrites { get; init; }

        public Guid GetActiveScheme()
        {
            Calls.Add("Get");
            return SchemeGuid;
        }

        public uint ReadAcValue(Guid schemeGuid)
        {
            Assert.Equal(SchemeGuid, schemeGuid);
            Calls.Add("ReadAc");
            return AcSeconds;
        }

        public uint ReadDcValue(Guid schemeGuid)
        {
            Assert.Equal(SchemeGuid, schemeGuid);
            Calls.Add("ReadDc");
            return DcSeconds;
        }

        public void WriteAcValue(Guid schemeGuid, uint seconds)
        {
            Assert.Equal(SchemeGuid, schemeGuid);
            Calls.Add($"WriteAc:{seconds}");
            if (!IgnoreWrites)
            {
                AcSeconds = seconds;
            }
        }

        public void WriteDcValue(Guid schemeGuid, uint seconds)
        {
            Assert.Equal(SchemeGuid, schemeGuid);
            Calls.Add($"WriteDc:{seconds}");
            if (!IgnoreWrites)
            {
                DcSeconds = seconds;
            }
        }

        public void Activate(Guid schemeGuid)
        {
            Assert.Equal(SchemeGuid, schemeGuid);
            Calls.Add("Activate");
        }
    }
}
