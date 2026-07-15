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
        Assert.Equal(3, viewModel.SelectedAcSliderValue);
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

        public Task<DisplayTimeoutValues> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(values);

        public Task<DisplayTimeoutValues> SetAsync(
            PowerSettingTarget target,
            DisplayTimeoutPreset preset,
            CancellationToken cancellationToken = default)
        {
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
