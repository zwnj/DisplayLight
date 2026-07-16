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
    private readonly DispatcherTimer statusClearTimer;
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
    private int? currentAcPresetIndex;
    private int? currentBatteryPresetIndex;
    private PowerSettingTarget? expandedTarget = PowerSettingTarget.AcPower;
    private bool hasInitializedExpansion;
    private bool isUpdatingSelection;
    private bool hasAcSelectionChanged;
    private bool hasBatterySelectionChanged;
    private string acErrorMessage = string.Empty;
    private string batteryErrorMessage = string.Empty;
    private string sleepErrorMessage = string.Empty;
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
        statusClearTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(4),
        };
        statusClearTimer.Tick += HandleStatusClearTimerTick;
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
                if (!isUpdatingSelection)
                {
                    hasAcSelectionChanged = true;
                }

                SelectedAcText = PresetPresentation.GetLabel(GetPreset(normalized));
                OnPropertyChanged(nameof(SelectedAcPresetIndex));
                OnPropertyChanged(nameof(HasPendingAcChange));
                OnPropertyChanged(nameof(AcSelectionCaption));
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
                if (!isUpdatingSelection)
                {
                    hasBatterySelectionChanged = true;
                }

                SelectedBatteryText = PresetPresentation.GetLabel(GetPreset(normalized));
                OnPropertyChanged(nameof(SelectedBatteryPresetIndex));
                OnPropertyChanged(nameof(HasPendingBatteryChange));
                OnPropertyChanged(nameof(BatterySelectionCaption));
            }
        }
    }

    public int SelectedAcPresetIndex
    {
        get => (int)SelectedAcSliderValue;
        set => SelectedAcSliderValue = value;
    }

    public int SelectedBatteryPresetIndex
    {
        get => (int)SelectedBatterySliderValue;
        set => SelectedBatterySliderValue = value;
    }

    public bool HasPendingAcChange =>
        hasAcSelectionChanged && currentAcPresetIndex != SelectedAcPresetIndex;

    public bool HasPendingBatteryChange =>
        hasBatterySelectionChanged && currentBatteryPresetIndex != SelectedBatteryPresetIndex;

    public string AcSelectionCaption => GetSelectionCaption(HasPendingAcChange, currentAcPresetIndex);

    public string BatterySelectionCaption => GetSelectionCaption(HasPendingBatteryChange, currentBatteryPresetIndex);

    public int? CurrentAcPresetIndex => currentAcPresetIndex;

    public int? CurrentBatteryPresetIndex => currentBatteryPresetIndex;

    public bool IsAcExpanded => expandedTarget == PowerSettingTarget.AcPower;

    public bool IsBatteryExpanded => expandedTarget == PowerSettingTarget.Battery;

    public string AcExpansionAutomationName =>
        $"AC電源時、現在{CurrentAcText}、{(IsAcExpanded ? "折りたたむ" : "展開")}";

    public string BatteryExpansionAutomationName =>
        $"バッテリー時、現在{CurrentBatteryText}、{(IsBatteryExpanded ? "折りたたむ" : "展開")}";

    public string AcErrorMessage
    {
        get => acErrorMessage;
        private set
        {
            if (SetProperty(ref acErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasAcError));
            }
        }
    }

    public string BatteryErrorMessage
    {
        get => batteryErrorMessage;
        private set
        {
            if (SetProperty(ref batteryErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasBatteryError));
            }
        }
    }

    public string SleepErrorMessage
    {
        get => sleepErrorMessage;
        private set
        {
            if (SetProperty(ref sleepErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasSleepError));
            }
        }
    }

    public bool HasAcError => !string.IsNullOrEmpty(AcErrorMessage);

    public bool HasBatteryError => !string.IsNullOrEmpty(BatteryErrorMessage);

    public bool HasSleepError => !string.IsNullOrEmpty(SleepErrorMessage);

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public string CurrentAcText
    {
        get => currentAcText;
        private set
        {
            if (SetProperty(ref currentAcText, value))
            {
                OnPropertyChanged(nameof(AcExpansionAutomationName));
                OnPropertyChanged(nameof(CurrentAcDisplayText));
                OnPropertyChanged(nameof(IsCurrentAcCustom));
            }
        }
    }

    public string CurrentBatteryText
    {
        get => currentBatteryText;
        private set
        {
            if (SetProperty(ref currentBatteryText, value))
            {
                OnPropertyChanged(nameof(BatteryExpansionAutomationName));
                OnPropertyChanged(nameof(CurrentBatteryDisplayText));
                OnPropertyChanged(nameof(IsCurrentBatteryCustom));
            }
        }
    }

    public string CurrentAcDisplayText => RemoveCustomPresetSuffix(CurrentAcText);

    public string CurrentBatteryDisplayText => RemoveCustomPresetSuffix(CurrentBatteryText);

    public bool IsCurrentAcCustom => CurrentAcText.Contains("（プリセット外）", StringComparison.Ordinal);

    public bool IsCurrentBatteryCustom => CurrentBatteryText.Contains("（プリセット外）", StringComparison.Ordinal);

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
        private set
        {
            if (SetProperty(ref sleepPreventionStatusText, value))
            {
                OnPropertyChanged(nameof(SleepPreventionAutomationName));
            }
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            if (SetProperty(ref statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
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
                OnPropertyChanged(nameof(SleepPreventionAutomationName));
            }
        }
    }

    public bool IsSleepPreventionActive
    {
        get => isSleepPreventionActive;
        private set => SetProperty(ref isSleepPreventionActive, value);
    }

    public string SleepToggleButtonText => IsSleepPreventionRequested ? "スリープ防止を解除" : "スリープ防止を開始";

    public string SleepPreventionAutomationName =>
        $"スリープ防止、{SleepPreventionStatusText}。押すと{(IsSleepPreventionRequested ? "解除" : "開始")}します";

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
        UpdateSelectionWithoutMarkingPending(() =>
        {
            SelectedAcSliderValue = GetSliderIndex(settings.SelectedAcTimeout);
            SelectedBatterySliderValue = GetSliderIndex(settings.SelectedBatteryTimeout);
        });

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
            SetTransientStatus("スリープ復帰後に防止要求を更新しました。");
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
        statusClearTimer.Stop();
        statusClearTimer.Tick -= HandleStatusClearTimerTick;
    }

    internal void Expand(PowerSettingTarget target)
    {
        SetExpandedTarget(expandedTarget == target ? null : target);
    }

    private void SetExpandedTarget(PowerSettingTarget? target)
    {
        expandedTarget = target;
        OnPropertyChanged(nameof(IsAcExpanded));
        OnPropertyChanged(nameof(IsBatteryExpanded));
        OnPropertyChanged(nameof(AcExpansionAutomationName));
        OnPropertyChanged(nameof(BatteryExpansionAutomationName));
    }

    private async Task ApplyTimeoutAsync(PowerSettingTarget target)
    {
        ClearTargetError(target);
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
            ClearSelectionChanged(target);
            SetTransientStatus($"{GetTargetLabel(target)}の消灯時間を{PresetPresentation.GetLabel(preset)}に変更しました。");
        }, exception => SetTargetError(target, exception));
    }

    private async Task RefreshAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            DisplayTimeoutValues values = await displayTimeoutService.ReadAsync();
            ApplyCurrentValues(values, updateSlidersForExactValues: true);
            hasAcSelectionChanged = false;
            hasBatterySelectionChanged = false;
            NotifySelectionStateChanged();
            RefreshPowerSource(updateStatusMessage: false);
            AcErrorMessage = string.Empty;
            BatteryErrorMessage = string.Empty;
            SetTransientStatus("Windowsの現在の電源設定を読み込みました。");
        });
    }

    private Task ToggleSleepPreventionAsync()
    {
        SleepErrorMessage = string.Empty;
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
        }, exception => SleepErrorMessage = FormatError(exception));
    }

    private async Task RunBusyOperationAsync(Func<Task> operation, Action<Exception>? onError = null)
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
            onError?.Invoke(exception);
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

        currentAcPresetIndex = DisplayTimeoutCatalog.TryFromSeconds(values.AcSeconds, out DisplayTimeoutPreset acPreset)
            ? GetSliderIndex(acPreset)
            : null;
        currentBatteryPresetIndex = DisplayTimeoutCatalog.TryFromSeconds(values.BatterySeconds, out DisplayTimeoutPreset batteryPreset)
            ? GetSliderIndex(batteryPreset)
            : null;

        OnPropertyChanged(nameof(CurrentAcPresetIndex));
        OnPropertyChanged(nameof(CurrentBatteryPresetIndex));

        UpdateSelectionWithoutMarkingPending(() =>
        {
            if (updateSlidersForExactValues && currentAcPresetIndex is int acIndex)
            {
                SelectedAcSliderValue = acIndex;
            }

            if (updateSlidersForExactValues && currentBatteryPresetIndex is int batteryIndex)
            {
                SelectedBatterySliderValue = batteryIndex;
            }
        });

        NotifySelectionStateChanged();
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
                PowerSource.AcPower => "電源：AC接続",
                PowerSource.Battery => "電源：バッテリー駆動",
                _ => "電源：不明",
            };

            if (!hasInitializedExpansion && source is PowerSource.AcPower or PowerSource.Battery)
            {
                SetExpandedTarget(source == PowerSource.Battery
                    ? PowerSettingTarget.Battery
                    : PowerSettingTarget.AcPower);
                hasInitializedExpansion = true;
            }

            if (changed || updateStatusMessage)
            {
                ApplySleepPreventionDecision();
            }
        }
        catch (Exception exception)
        {
            currentPowerSource = PowerSource.Unknown;
            PowerSourceText = "電源：取得失敗";
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
                SleepPreventionReason.Active => "オン",
                SleepPreventionReason.WaitingForAcPower => "AC電源接続待ち",
                SleepPreventionReason.PowerSourceUnavailable => "電源状態不明のため解除",
                _ => "オフ",
            };
            SetTransientStatus(decision.Reason switch
            {
                SleepPreventionReason.Active => "システムスリープを防止しています。",
                SleepPreventionReason.WaitingForAcPower => "バッテリー駆動へ切り替わったため、スリープ防止を解除しました。",
                SleepPreventionReason.PowerSourceUnavailable => "電源状態を確認できないため、スリープ防止を解除しました。",
                _ => "Windowsの通常の電源管理を使用しています。",
            });
            HasError = false;
            SleepErrorMessage = string.Empty;
        }
        catch (Exception exception)
        {
            IsSleepPreventionRequested = false;
            IsSleepPreventionActive = false;
            SleepPreventionStatusText = "エラーのため解除";
            SetError(exception);
            SleepErrorMessage = FormatError(exception);
        }
    }

    private void HandlePowerSourceTimerTick(object? sender, EventArgs e) =>
        RefreshPowerSource(updateStatusMessage: false);

    private void SetError(Exception exception)
    {
        HasError = true;
        StatusMessage = FormatError(exception);
        statusClearTimer.Stop();
    }

    private void SetTransientStatus(string message)
    {
        StatusMessage = message;
        statusClearTimer.Stop();
        statusClearTimer.Start();
    }

    private void HandleStatusClearTimerTick(object? sender, EventArgs e)
    {
        statusClearTimer.Stop();
        if (!HasError)
        {
            StatusMessage = string.Empty;
        }
    }

    private void ClearTargetError(PowerSettingTarget target)
    {
        if (target == PowerSettingTarget.AcPower)
        {
            AcErrorMessage = string.Empty;
        }
        else
        {
            BatteryErrorMessage = string.Empty;
        }
    }

    private void SetTargetError(PowerSettingTarget target, Exception exception)
    {
        if (target == PowerSettingTarget.AcPower)
        {
            AcErrorMessage = FormatError(exception);
        }
        else
        {
            BatteryErrorMessage = FormatError(exception);
        }
    }

    private static string FormatError(Exception exception) => exception is Win32Exception win32Exception
        ? $"{win32Exception.Message}（Windowsエラー {win32Exception.NativeErrorCode}）"
        : exception.Message;

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

    private void UpdateSelectionWithoutMarkingPending(Action update)
    {
        isUpdatingSelection = true;
        try
        {
            update();
        }
        finally
        {
            isUpdatingSelection = false;
        }
    }

    private void ClearSelectionChanged(PowerSettingTarget target)
    {
        if (target == PowerSettingTarget.AcPower)
        {
            hasAcSelectionChanged = false;
        }
        else
        {
            hasBatterySelectionChanged = false;
        }

        NotifySelectionStateChanged();
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(HasPendingAcChange));
        OnPropertyChanged(nameof(HasPendingBatteryChange));
        OnPropertyChanged(nameof(AcSelectionCaption));
        OnPropertyChanged(nameof(BatterySelectionCaption));
    }

    private static string RemoveCustomPresetSuffix(string value) =>
        value.Replace("（プリセット外）", string.Empty, StringComparison.Ordinal);

    private static string GetSelectionCaption(bool hasPendingChange, int? currentPresetIndex) =>
        hasPendingChange ? "変更後" : currentPresetIndex is null ? "前回選択" : "現在";

    private static string GetTargetLabel(PowerSettingTarget target) => target switch
    {
        PowerSettingTarget.AcPower => "AC電源時",
        PowerSettingTarget.Battery => "バッテリー時",
        _ => "不明な電源状態",
    };
}
