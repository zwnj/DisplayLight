using System.ComponentModel;
using DisplayLight.App.Presentation;
using DisplayLight.Core.Abstractions;
using DisplayLight.Core.Power;
using DisplayLight.Core.Settings;

namespace DisplayLight.App.Tests.Presentation;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task InitializeAsyncPreservesSelectionForCustomCurrentValue()
    {
        FakeDisplayTimeoutService displayService = new(new DisplayTimeoutValues(15 * 60, 5 * 60));
        FakeSleepPreventionService sleepService = new();
        FakeSettingsStore settingsStore = new(new UserSettings
        {
            SelectedAcTimeout = DisplayTimeoutPreset.ThirtyMinutes,
            SelectedBatteryTimeout = DisplayTimeoutPreset.TenMinutes,
        });
        using MainWindowViewModel viewModel = new(
            displayService,
            sleepService,
            new FakePowerSourceProvider(PowerSource.AcPower),
            settingsStore);

        await viewModel.InitializeAsync();

        Assert.Equal("15分（プリセット外）", viewModel.CurrentAcText);
        Assert.Equal("15分", viewModel.CurrentAcDisplayText);
        Assert.True(viewModel.IsCurrentAcCustom);
        Assert.Equal(3, viewModel.SelectedAcSliderValue);
        Assert.False(viewModel.HasPendingAcChange);
        Assert.True(viewModel.HasAcSelectionSummary);
        Assert.Equal("前回選択", viewModel.AcSelectionCaption);
        Assert.Equal("5分", viewModel.CurrentBatteryText);
        Assert.Equal(1, viewModel.SelectedBatterySliderValue);
    }

    [Fact]
    public async Task ToggleSleepPreventionKeepsIntentButReleasesRequestOnBatteryWhenAcOnly()
    {
        FakeSleepPreventionService sleepService = new();
        FakeSettingsStore settingsStore = new(new UserSettings { IsAcPowerOnly = true });
        using MainWindowViewModel viewModel = new(
            new FakeDisplayTimeoutService(new DisplayTimeoutValues(600, 600)),
            sleepService,
            new FakePowerSourceProvider(PowerSource.Battery),
            settingsStore);
        await viewModel.InitializeAsync();
        AsyncRelayCommand command = Assert.IsType<AsyncRelayCommand>(viewModel.ToggleSleepPreventionCommand);

        await command.ExecuteAsync();

        Assert.True(viewModel.IsSleepPreventionRequested);
        Assert.False(viewModel.IsSleepPreventionActive);
        Assert.Equal("AC電源接続待ち", viewModel.SleepPreventionStatusText);
        Assert.Equal("スリープ防止、AC電源接続待ち。押すと解除します", viewModel.SleepPreventionAutomationName);
        Assert.False(sleepService.IsActive);
    }

    [Fact]
    public async Task ApplyAcCommandPersistsSelectionAfterVerifiedWrite()
    {
        FakeDisplayTimeoutService displayService = new(new DisplayTimeoutValues(600, 300));
        FakeSettingsStore settingsStore = new(new UserSettings());
        using MainWindowViewModel viewModel = new(
            displayService,
            new FakeSleepPreventionService(),
            new FakePowerSourceProvider(PowerSource.AcPower),
            settingsStore);
        await viewModel.InitializeAsync();
        viewModel.SelectedAcSliderValue = 4;
        viewModel.SelectedBatterySliderValue = 5;
        AsyncRelayCommand command = Assert.IsType<AsyncRelayCommand>(viewModel.ApplyAcCommand);

        await command.ExecuteAsync();

        Assert.Equal(PowerSettingTarget.AcPower, displayService.LastTarget);
        Assert.Equal(DisplayTimeoutPreset.SixtyMinutes, displayService.LastPreset);
        Assert.Equal(DisplayTimeoutPreset.SixtyMinutes, settingsStore.LastSaved?.SelectedAcTimeout);
        Assert.Equal("60分", viewModel.CurrentAcText);
        Assert.Equal(5, viewModel.SelectedBatterySliderValue);
        Assert.False(viewModel.HasPendingAcChange);
        Assert.True(viewModel.HasPendingBatteryChange);
    }

    [Fact]
    public async Task InitializesExpandedSectionFromCurrentPowerSource()
    {
        using MainWindowViewModel viewModel = new(
            new FakeDisplayTimeoutService(new DisplayTimeoutValues(600, 300)),
            new FakeSleepPreventionService(),
            new FakePowerSourceProvider(PowerSource.Battery),
            new FakeSettingsStore(new UserSettings()));

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsAcExpanded);
        Assert.True(viewModel.IsBatteryExpanded);
        Assert.Equal("電源：バッテリー駆動", viewModel.PowerSourceText);
        Assert.Equal("AC電源時、現在10分、展開", viewModel.AcExpansionAutomationName);
        Assert.Equal("バッテリー時、現在5分、折りたたむ", viewModel.BatteryExpansionAutomationName);
        Assert.Equal("⌄", viewModel.AcChevronText);
        Assert.Equal("⌃", viewModel.BatteryChevronText);
    }

    [Fact]
    public async Task ExpandedSectionCanBeCollapsedAndReopened()
    {
        using MainWindowViewModel viewModel = new(
            new FakeDisplayTimeoutService(new DisplayTimeoutValues(600, 300)),
            new FakeSleepPreventionService(),
            new FakePowerSourceProvider(PowerSource.AcPower),
            new FakeSettingsStore(new UserSettings()));
        await viewModel.InitializeAsync();

        viewModel.Expand(PowerSettingTarget.AcPower);

        Assert.False(viewModel.IsAcExpanded);
        Assert.False(viewModel.IsBatteryExpanded);
        Assert.Equal("AC電源時、現在10分、展開", viewModel.AcExpansionAutomationName);

        viewModel.Expand(PowerSettingTarget.Battery);

        Assert.False(viewModel.IsAcExpanded);
        Assert.True(viewModel.IsBatteryExpanded);
    }

    [Fact]
    public async Task PendingStateOnlyAppearsWhenSelectionDiffersFromCurrentValue()
    {
        using MainWindowViewModel viewModel = new(
            new FakeDisplayTimeoutService(new DisplayTimeoutValues(600, 300)),
            new FakeSleepPreventionService(),
            new FakePowerSourceProvider(PowerSource.AcPower),
            new FakeSettingsStore(new UserSettings()));
        await viewModel.InitializeAsync();

        Assert.False(viewModel.HasPendingAcChange);
        Assert.Equal(2, viewModel.CurrentAcPresetIndex);
        viewModel.SelectedAcPresetIndex = 4;
        Assert.True(viewModel.HasPendingAcChange);
        Assert.Equal("変更後", viewModel.AcSelectionCaption);
        viewModel.SelectedAcPresetIndex = 2;
        Assert.False(viewModel.HasPendingAcChange);
    }

    [Fact]
    public async Task ApplyFailureRemainsOnTheAffectedSection()
    {
        FakeDisplayTimeoutService displayService = new(new DisplayTimeoutValues(600, 300))
        {
            SetException = new Win32Exception(5, "AC設定を変更できませんでした。"),
        };
        using MainWindowViewModel viewModel = new(
            displayService,
            new FakeSleepPreventionService(),
            new FakePowerSourceProvider(PowerSource.AcPower),
            new FakeSettingsStore(new UserSettings()));
        await viewModel.InitializeAsync();
        viewModel.SelectedAcPresetIndex = 4;
        AsyncRelayCommand command = Assert.IsType<AsyncRelayCommand>(viewModel.ApplyAcCommand);

        await command.ExecuteAsync();

        Assert.True(viewModel.HasAcError);
        Assert.False(viewModel.HasBatteryError);
        Assert.Contains("Windowsエラー 5", viewModel.AcErrorMessage, StringComparison.Ordinal);
        Assert.True(viewModel.HasPendingAcChange);
    }

    [Fact]
    public async Task CommandsRemainDisabledUntilInitializationCompletes()
    {
        TaskCompletionSource<UserSettings> loadCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using MainWindowViewModel viewModel = new(
            new FakeDisplayTimeoutService(new DisplayTimeoutValues(600, 600)),
            new FakeSleepPreventionService(),
            new FakePowerSourceProvider(PowerSource.AcPower),
            new DelayedSettingsStore(loadCompletion.Task));

        Task initialization = viewModel.InitializeAsync();

        Assert.False(viewModel.ApplyAcCommand.CanExecute(parameter: null));
        Assert.False(viewModel.ToggleSleepPreventionCommand.CanExecute(parameter: null));

        loadCompletion.SetResult(new UserSettings());
        await initialization;

        Assert.True(viewModel.ApplyAcCommand.CanExecute(parameter: null));
        Assert.True(viewModel.ToggleSleepPreventionCommand.CanExecute(parameter: null));
    }

    private sealed class FakeDisplayTimeoutService(DisplayTimeoutValues values) : IDisplayTimeoutService
    {
        private DisplayTimeoutValues values = values;

        public PowerSettingTarget LastTarget { get; private set; }

        public DisplayTimeoutPreset LastPreset { get; private set; }

        public Exception? SetException { get; init; }

        public Task<DisplayTimeoutValues> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(values);

        public Task<DisplayTimeoutValues> SetAsync(
            PowerSettingTarget target,
            DisplayTimeoutPreset preset,
            CancellationToken cancellationToken = default)
        {
            if (SetException is not null)
            {
                throw SetException;
            }

            LastTarget = target;
            LastPreset = preset;
            uint seconds = DisplayTimeoutCatalog.ToSeconds(preset);
            values = target == PowerSettingTarget.AcPower
                ? values with { AcSeconds = seconds }
                : values with { BatterySeconds = seconds };
            return Task.FromResult(values);
        }
    }

    private sealed class FakeSleepPreventionService : ISleepPreventionService
    {
        public bool IsActive { get; private set; }

        public void SetActive(bool isActive) => IsActive = isActive;

        public void Renew()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakePowerSourceProvider(PowerSource source) : IPowerSourceProvider
    {
        public PowerSource GetCurrent() => source;
    }

    private sealed class FakeSettingsStore(UserSettings settings) : IUserSettingsStore
    {
        public UserSettings? LastSaved { get; private set; }

        public Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
        {
            LastSaved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class DelayedSettingsStore(Task<UserSettings> settingsTask) : IUserSettingsStore
    {
        public Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default) => settingsTask;

        public Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
