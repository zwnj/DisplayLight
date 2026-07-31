using DisplayLight.App.Infrastructure.Windows;
using DisplayLight.Core.Power;

namespace DisplayLight.App.Tests.Infrastructure.Windows;

public sealed class WindowsDisplayOffServiceTests
{
    private static readonly Guid SchemeGuid = new("d6e6a9cb-109f-40f8-922f-6d5602c301c9");

    [Fact]
    public async Task TurnOffAsyncTemporarilyChangesOnlyCurrentDcDisplayTimeout()
    {
        FakePowerSchemeNativeApi nativeApi = new();
        FakeRecoveryStore recoveryStore = new();
        using WindowsDisplayOffService service = new(
            nativeApi,
            new FakePowerSourceProvider(PowerSource.Battery),
            recoveryStore,
            (_, _) => Task.CompletedTask);

        await service.TurnOffAsync();

        Assert.Equal(1800u, nativeApi.DcDisplaySeconds);
        Assert.Equal(1500u, nativeApi.AcSleepSeconds);
        Assert.Equal(3600u, nativeApi.DcSleepSeconds);
        Assert.Equal(
            [
                "Get",
                "ReadDcDisplay",
                "WriteDcDisplay:1",
                "Activate",
                "ReadDcDisplay",
                "WriteDcDisplay:1800",
                "Get",
                "Activate",
                "ReadDcDisplay",
            ],
            nativeApi.Calls);
        Assert.Equal(1, recoveryStore.SaveCount);
        Assert.Equal(1, recoveryStore.DeleteCount);
    }

    [Fact]
    public async Task TurnOffAsyncUsesAcValueWhenConnectedToAcPower()
    {
        FakePowerSchemeNativeApi nativeApi = new();
        using WindowsDisplayOffService service = new(
            nativeApi,
            new FakePowerSourceProvider(PowerSource.AcPower),
            new FakeRecoveryStore(),
            (_, _) => Task.CompletedTask);

        await service.TurnOffAsync();

        Assert.Contains("WriteAcDisplay:1", nativeApi.Calls);
        Assert.Contains("WriteAcDisplay:300", nativeApi.Calls);
        Assert.DoesNotContain(
            nativeApi.Calls,
            call => call.StartsWith("WriteDc", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TurnOffAsyncDoesNotOverwriteAConcurrentExternalChange()
    {
        FakePowerSchemeNativeApi nativeApi = new();
        using WindowsDisplayOffService service = new(
            nativeApi,
            new FakePowerSourceProvider(PowerSource.Battery),
            new FakeRecoveryStore(),
            (_, _) =>
            {
                nativeApi.DcDisplaySeconds = 30;
                return Task.CompletedTask;
            });

        await service.TurnOffAsync();

        Assert.Equal(30u, nativeApi.DcDisplaySeconds);
        Assert.DoesNotContain("WriteDcDisplay:1800", nativeApi.Calls);
    }

    [Fact]
    public async Task TurnOffAsyncDoesNotReactivateAPlanChangedDuringTheLease()
    {
        FakePowerSchemeNativeApi nativeApi = new();
        Guid otherSchemeGuid = Guid.NewGuid();
        using WindowsDisplayOffService service = new(
            nativeApi,
            new FakePowerSourceProvider(PowerSource.Battery),
            new FakeRecoveryStore(),
            (_, _) =>
            {
                nativeApi.ActiveSchemeGuid = otherSchemeGuid;
                return Task.CompletedTask;
            });

        await service.TurnOffAsync();

        Assert.Equal(1800u, nativeApi.DcDisplaySeconds);
        Assert.Equal(otherSchemeGuid, nativeApi.ActiveSchemeGuid);
        Assert.Equal(1, nativeApi.Calls.Count(call => call == "Activate"));
    }

    [Fact]
    public async Task TurnOffAsyncRestoresTheOriginalValueWhenActivationFails()
    {
        FakePowerSchemeNativeApi nativeApi = new()
        {
            FailNextActivation = true,
        };
        using WindowsDisplayOffService service = new(
            nativeApi,
            new FakePowerSourceProvider(PowerSource.Battery),
            new FakeRecoveryStore(),
            (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TurnOffAsync());

        Assert.Equal(1800u, nativeApi.DcDisplaySeconds);
        Assert.Contains("WriteDcDisplay:1800", nativeApi.Calls);
    }

    [Fact]
    public async Task TurnOffAsyncRejectsUnknownPowerSourceBeforeChangingSettings()
    {
        FakePowerSchemeNativeApi nativeApi = new();
        using WindowsDisplayOffService service = new(
            nativeApi,
            new FakePowerSourceProvider(PowerSource.Unknown),
            new FakeRecoveryStore(),
            (_, _) => Task.CompletedTask);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TurnOffAsync());

        Assert.Contains("電源状態", exception.Message, StringComparison.Ordinal);
        Assert.Empty(nativeApi.Calls);
    }

    [Fact]
    public async Task DisposeRestoresAnActiveTemporaryValue()
    {
        FakePowerSchemeNativeApi nativeApi = new();
        WindowsDisplayOffService service = new(
            nativeApi,
            new FakePowerSourceProvider(PowerSource.Battery),
            new FakeRecoveryStore(),
            (_, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

        Task operation = service.TurnOffAsync();
        Assert.Equal(1u, nativeApi.DcDisplaySeconds);

        service.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(1800u, nativeApi.DcDisplaySeconds);
    }

    [Fact]
    public void RecoverPendingChangeRestoresAValueLeftByAnInterruptedOperation()
    {
        FakePowerSchemeNativeApi nativeApi = new()
        {
            DcDisplaySeconds = WindowsDisplayOffService.TemporaryDisplayTimeoutSeconds,
        };
        FakeRecoveryStore recoveryStore = new()
        {
            State = CreateRecoveryState(),
        };
        using WindowsDisplayOffService service = new(
            nativeApi,
            new FakePowerSourceProvider(PowerSource.Battery),
            recoveryStore,
            (_, _) => Task.CompletedTask);

        service.RecoverPendingChange();

        Assert.Equal(1800u, nativeApi.DcDisplaySeconds);
        Assert.Null(recoveryStore.State);
        Assert.Equal(1, recoveryStore.DeleteCount);
    }

    [Fact]
    public void RecoverPendingChangePreservesANewerExternalValue()
    {
        FakePowerSchemeNativeApi nativeApi = new()
        {
            DcDisplaySeconds = 30,
        };
        FakeRecoveryStore recoveryStore = new()
        {
            State = CreateRecoveryState(),
        };
        using WindowsDisplayOffService service = new(
            nativeApi,
            new FakePowerSourceProvider(PowerSource.Battery),
            recoveryStore,
            (_, _) => Task.CompletedTask);

        service.RecoverPendingChange();

        Assert.Equal(30u, nativeApi.DcDisplaySeconds);
        Assert.DoesNotContain("WriteDcDisplay:1800", nativeApi.Calls);
        Assert.Null(recoveryStore.State);
    }

    private static DisplayOffRecoveryState CreateRecoveryState() => new(
        SchemeGuid,
        PowerSettingTarget.Battery,
        OriginalSeconds: 1800,
        TemporarySeconds: WindowsDisplayOffService.TemporaryDisplayTimeoutSeconds,
        CreatedAt: DateTimeOffset.UtcNow);

    private sealed class FakePowerSchemeNativeApi : IPowerSchemeNativeApi
    {
        public List<string> Calls { get; } = [];

        public uint AcDisplaySeconds { get; set; } = 300;

        public uint DcDisplaySeconds { get; set; } = 1800;

        public uint AcSleepSeconds { get; set; } = 1500;

        public uint DcSleepSeconds { get; set; } = 3600;

        public bool FailNextActivation { get; set; }

        public Guid ActiveSchemeGuid { get; set; } = SchemeGuid;

        public Guid GetActiveScheme()
        {
            Calls.Add("Get");
            return ActiveSchemeGuid;
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
            AcSleepSeconds = seconds;
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
            if (FailNextActivation)
            {
                FailNextActivation = false;
                throw new InvalidOperationException("Activation failed.");
            }

            ActiveSchemeGuid = schemeGuid;
        }

        private static void AssertScheme(Guid schemeGuid) => Assert.Equal(SchemeGuid, schemeGuid);
    }

    private sealed class FakePowerSourceProvider(PowerSource source) : Core.Abstractions.IPowerSourceProvider
    {
        public PowerSource GetCurrent() => source;
    }

    private sealed class FakeRecoveryStore : IDisplayOffRecoveryStore
    {
        public DisplayOffRecoveryState? State { get; set; }

        public int DeleteCount { get; private set; }

        public int SaveCount { get; private set; }

        public DisplayOffRecoveryState? Load() => State;

        public void Save(DisplayOffRecoveryState state)
        {
            SaveCount++;
            State = state;
        }

        public void Delete()
        {
            DeleteCount++;
            State = null;
        }
    }
}
