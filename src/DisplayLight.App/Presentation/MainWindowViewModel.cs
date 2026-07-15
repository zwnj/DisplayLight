using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using DisplayLight.Core.Abstractions;
using DisplayLight.Core.Power;
using DisplayLight.Core.Settings;

namespace DisplayLight.App.Presentation;

internal sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IDisplayTimeoutService displayTimeoutService;
    private readonly ISleepPreventionService sleepPreventionService;
    private readonly IPowerSourceProvider powerSourceProvider;
    private readonly IUserSettingsStore settingsStore;
    private readonly DispatcherTimer powerSourceTimer;
    private readonly AsyncRelayCommand applyAcCommand;
    private readonly AsyncRelayCommand applyBatteryCommand;
    private readonly AsyncRelayCommand refreshCommand;
    private readonly AsyncRelayCommand toggleSleepPreventionCommand;
    private readonly AsyncRelayCommand updateAcPowerOnlyCommand;

    private UserSettings settings = new();
    private double selectedAcSliderValue = 2;
    private double selectedBatterySliderValue = 2;
    private string currentAcText = "読み込み中";
    private string currentBatteryText = "読み込み中";
    private string selectedAcText = "10分";
    private string selectedBatteryText = "10分";
    private string powerSourceText = "確認中";
    private string sleepPreventionStatusText = "無効";
    private string statusMessage = "設定を読み込んでいます。";
    private bool isAcPowerOnly;
    private bool isSleepPreventionRequested;
    private bool isSleepPreventionActive;
    private bool isBusy = true;
    private bool hasError;
    private bool isDisposed;
    private PowerSource currentPowerSource = PowerSource.Unknown;

    public MainWindowViewModel(
        IDisplayTimeoutService displayTimeoutService,
        ISleepPreventionService sleepPreventionService,
        IPowerSourceProvider powerSourceProvider,
        IUserSettingsStore settingsStore)
    {
        this.displayTimeoutService = displayTimeoutService;
        this.sleepPreventionService = sleepPreventionService;
        this.powerSourceProvider = powerSourceProvider;
        this.settingsStore = settingsStore;

        applyAcCommand = new(() => ApplyTimeoutAsync(PowerSettingTarget.AcPower), () => !IsBusy);
        applyBatteryCommand = new(() => ApplyTimeoutAsync(PowerSettingTarget.Battery), () => !IsBusy);
        refreshCommand = new(RefreshAsync, () => !IsBusy);
        toggleSleepPreventionCommand = new(ToggleSleepPreventionAsync, () => !IsBusy);
        updateAcPowerOnlyCommand = new(UpdateAcPowerOnlyAsync, () => !IsBusy);

        powerSourceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        powerSourceTimer.Tick += HandlePowerSourceTimerTick;
    }

    public ICommand ApplyAcCommand => applyAcCommand;

    public ICommand ApplyBatteryCommand => applyBatteryCommand;

    public ICommand RefreshCommand => refreshCommand;

    public ICommand ToggleSleepPreventionCommand => toggleSleepPreventionCommand;

    public ICommand UpdateAcPowerOnlyCommand => updateAcPowerOnlyCommand;

    public double SelectedAcSliderValue
    {
        get => selectedAcSliderValue;
        set
        {
            double normalized = NormalizeSliderValue(value);
            if (SetProperty(ref selectedAcSliderValue, normalized))
            {
                SelectedAcText = PresetPresentation.GetLabel(GetPreset(normalized));
            }
        }
    }

    public double SelectedBatterySliderValue
    {
        get => selectedBatterySliderValue;
        set
        {
            double normalized = NormalizeSliderValue(value);
            if (SetProperty(ref selectedBatterySliderValue, normalized))
            {
                SelectedBatteryText = PresetPresentation.GetLabel(GetPreset(normalized));
            }
        }
    }

    public string CurrentAcText
    {
        get => currentAcText;
        private set => SetProperty(ref currentAcText, value);
    }

    public string CurrentBatteryText
    {
        get => currentBatteryText;
        private set => SetProperty(ref currentBatteryText, value);
    }

    public string SelectedAcText
    {
        get => selectedAcText;
        private set => SetProperty(ref selectedAcText, value);
    }

    public string SelectedBatteryText
    {
        get => selectedBatteryText;
        private set => SetProperty(ref selectedBatteryText, value);
    }

    public string PowerSourceText
    {
        get => powerSourceText;
        private set => SetProperty(ref powerSourceText, value);
    }

    public string SleepPreventionStatusText
    {
        get => sleepPreventionStatusText;
        private set => SetProperty(ref sleepPreventionStatusText, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public bool IsAcPowerOnly
    {
        get => isAcPowerOnly;
        set => SetProperty(ref isAcPowerOnly, value);
    }

    public bool IsSleepPreventionRequested
    {
        get => isSleepPreventionRequested;
        private set
        {
            if (SetProperty(ref isSleepPreventionRequested, value))
            {
                OnPropertyChanged(nameof(SleepToggleButtonText));
            }
        }
    }

    public bool IsSleepPreventionActive
    {
        get => isSleepPreventionActive;
        private set => SetProperty(ref isSleepPreventionActive, value);
    }

    public string SleepToggleButtonText => IsSleepPreventionRequested ? "スリープ防止を解除" : "スリープ防止を開始";

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                NotifyCommandsChanged();
            }
        }
    }

    public bool HasError
    {
        get => hasError;
        private set => SetProperty(ref hasError, value);
    }

    public async Task InitializeAsync()
    {
        settings = await settingsStore.LoadAsync();
        IsAcPowerOnly = settings.IsAcPowerOnly;
        SelectedAcSliderValue = GetSliderIndex(settings.SelectedAcTimeout);
        SelectedBatterySliderValue = GetSliderIndex(settings.SelectedBatteryTimeout);

        await RefreshAsync();
        powerSourceTimer.Start();
    }

    public void HandleSystemResume()
    {
        if (!IsSleepPreventionActive)
        {
            return;
        }

        try
        {
            sleepPreventionService.Renew();
            StatusMessage = "スリープ復帰後に防止要求を更新しました。";
            HasError = false;
        }
        catch (Exception exception)
        {
            IsSleepPreventionRequested = false;
            IsSleepPreventionActive = false;
            SetError(exception);
        }
    }

    public string GetTrayToolTip() => IsSleepPreventionActive
        ? "DisplayLight - スリープ防止中"
        : IsSleepPreventionRequested
            ? "DisplayLight - AC電源接続待ち"
            : "DisplayLight - 通常モード";

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        powerSourceTimer.Stop();
        powerSourceTimer.Tick -= HandlePowerSourceTimerTick;
    }

    private async Task ApplyTimeoutAsync(PowerSettingTarget target)
    {
        await RunBusyOperationAsync(async () =>
        {
            DisplayTimeoutPreset preset = target == PowerSettingTarget.AcPower
                ? GetPreset(SelectedAcSliderValue)
                : GetPreset(SelectedBatterySliderValue);
            DisplayTimeoutValues values = await displayTimeoutService.SetAsync(target, preset);
            ApplyCurrentValues(values, updateSlidersForExactValues: false);

            settings = target == PowerSettingTarget.AcPower
                ? settings with { SelectedAcTimeout = preset }
                : settings with { SelectedBatteryTimeout = preset };
            await settingsStore.SaveAsync(settings);
            StatusMessage = $"{GetTargetLabel(target)}の消灯時間を{PresetPresentation.GetLabel(preset)}に変更しました。";
        });
    }

    private async Task RefreshAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            DisplayTimeoutValues values = await displayTimeoutService.ReadAsync();
            ApplyCurrentValues(values, updateSlidersForExactValues: true);
            RefreshPowerSource(updateStatusMessage: false);
            StatusMessage = "Windowsの現在の電源設定を読み込みました。";
        });
    }

    private Task ToggleSleepPreventionAsync()
    {
        IsSleepPreventionRequested = !IsSleepPreventionRequested;
        ApplySleepPreventionDecision();
        return Task.CompletedTask;
    }

    private async Task UpdateAcPowerOnlyAsync()
    {
        settings = settings with { IsAcPowerOnly = IsAcPowerOnly };
        await RunBusyOperationAsync(async () =>
        {
            ApplySleepPreventionDecision();
            await settingsStore.SaveAsync(settings);
        });
    }

    private async Task RunBusyOperationAsync(Func<Task> operation)
    {
        IsBusy = true;
        HasError = false;

        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            SetError(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyCurrentValues(DisplayTimeoutValues values, bool updateSlidersForExactValues)
    {
        CurrentAcText = PresetPresentation.FormatCurrent(values.AcSeconds);
        CurrentBatteryText = PresetPresentation.FormatCurrent(values.BatterySeconds);

        if (updateSlidersForExactValues && DisplayTimeoutCatalog.TryFromSeconds(values.AcSeconds, out DisplayTimeoutPreset acPreset))
        {
            SelectedAcSliderValue = GetSliderIndex(acPreset);
        }

        if (updateSlidersForExactValues && DisplayTimeoutCatalog.TryFromSeconds(values.BatterySeconds, out DisplayTimeoutPreset batteryPreset))
        {
            SelectedBatterySliderValue = GetSliderIndex(batteryPreset);
        }
    }

    private void RefreshPowerSource(bool updateStatusMessage)
    {
        try
        {
            PowerSource source = powerSourceProvider.GetCurrent();
            bool changed = currentPowerSource != source;
            currentPowerSource = source;
            PowerSourceText = source switch
            {
                PowerSource.AcPower => "AC電源",
                PowerSource.Battery => "バッテリー",
                _ => "不明",
            };

            if (changed || updateStatusMessage)
            {
                ApplySleepPreventionDecision();
            }
        }
        catch (Exception exception)
        {
            currentPowerSource = PowerSource.Unknown;
            PowerSourceText = "取得失敗";
            ApplySleepPreventionDecision();
            if (updateStatusMessage)
            {
                SetError(exception);
            }
        }
    }

    private void ApplySleepPreventionDecision()
    {
        SleepPreventionDecision decision = SleepPreventionPolicy.Evaluate(
            IsSleepPreventionRequested,
            IsAcPowerOnly,
            currentPowerSource);

        try
        {
            sleepPreventionService.SetActive(decision.ShouldPreventSleep);
            IsSleepPreventionActive = sleepPreventionService.IsActive;
            SleepPreventionStatusText = decision.Reason switch
            {
                SleepPreventionReason.Active => "有効（ディスプレイ消灯は許可）",
                SleepPreventionReason.WaitingForAcPower => "AC電源接続待ち",
                SleepPreventionReason.PowerSourceUnavailable => "電源状態不明のため解除",
                _ => "無効",
            };
            StatusMessage = decision.Reason switch
            {
                SleepPreventionReason.Active => "システムスリープを防止しています。",
                SleepPreventionReason.WaitingForAcPower => "バッテリー駆動へ切り替わったため、スリープ防止を解除しました。",
                SleepPreventionReason.PowerSourceUnavailable => "電源状態を確認できないため、スリープ防止を解除しました。",
                _ => "Windowsの通常の電源管理を使用しています。",
            };
            HasError = false;
        }
        catch (Exception exception)
        {
            IsSleepPreventionRequested = false;
            IsSleepPreventionActive = false;
            SleepPreventionStatusText = "エラーのため解除";
            SetError(exception);
        }
    }

    private void HandlePowerSourceTimerTick(object? sender, EventArgs e) =>
        RefreshPowerSource(updateStatusMessage: false);

    private void SetError(Exception exception)
    {
        HasError = true;
        StatusMessage = exception is Win32Exception win32Exception
            ? $"{win32Exception.Message}（Windowsエラー {win32Exception.NativeErrorCode}）"
            : exception.Message;
    }

    private void NotifyCommandsChanged()
    {
        applyAcCommand.NotifyCanExecuteChanged();
        applyBatteryCommand.NotifyCanExecuteChanged();
        refreshCommand.NotifyCanExecuteChanged();
        toggleSleepPreventionCommand.NotifyCanExecuteChanged();
        updateAcPowerOnlyCommand.NotifyCanExecuteChanged();
    }

    private static double NormalizeSliderValue(double value) =>
        Math.Clamp(Math.Round(value), 0, DisplayTimeoutCatalog.All.Count - 1);

    private static DisplayTimeoutPreset GetPreset(double sliderValue) =>
        DisplayTimeoutCatalog.All[(int)NormalizeSliderValue(sliderValue)];

    private static int GetSliderIndex(DisplayTimeoutPreset preset)
    {
        for (int index = 0; index < DisplayTimeoutCatalog.All.Count; index++)
        {
            if (DisplayTimeoutCatalog.All[index] == preset)
            {
                return index;
            }
        }

        return 2;
    }

    private static string GetTargetLabel(PowerSettingTarget target) => target switch
    {
        PowerSettingTarget.AcPower => "AC電源時",
        PowerSettingTarget.Battery => "バッテリー時",
        _ => "不明な電源状態",
    };
}
