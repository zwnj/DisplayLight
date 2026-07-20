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
    private readonly IDisplayOffService displayOffService;
    private readonly IPowerSourceProvider powerSourceProvider;
    private readonly IUserSettingsStore settingsStore;
    private readonly IApplicationUpdateService? applicationUpdateService;
    private readonly DispatcherTimer powerSourceTimer;
    private readonly DispatcherTimer displayOffCountdownTimer;
    private readonly DisplayOffCountdown displayOffCountdown = new();
    private readonly AsyncRelayCommand applyAcCommand;
    private readonly AsyncRelayCommand applyBatteryCommand;
    private readonly AsyncRelayCommand refreshCommand;
    private readonly AsyncRelayCommand toggleSleepPreventionCommand;
    private readonly AsyncRelayCommand updateAcPowerOnlyCommand;
    private readonly AsyncRelayCommand toggleDisplayOffCountdownCommand;
    private readonly AsyncRelayCommand checkForUpdatesCommand;
    private readonly AsyncRelayCommand applyUpdateCommand;

    private UserSettings settings = new();
    private double selectedAcSliderValue = 2;
    private double selectedBatterySliderValue = 2;
    private string currentAcText = "読み込み中";
    private string currentBatteryText = "読み込み中";
    private string selectedAcText = "10分";
    private string selectedBatteryText = "10分";
    private string powerSourceText = "確認中";
    private string sleepPreventionStatusText = "無効";
    private string generalErrorMessage = string.Empty;
    private string displayOffErrorMessage = string.Empty;
    private bool isAcPowerOnly;
    private bool isSleepPreventionRequested;
    private bool isSleepPreventionActive;
    private bool isBusy = true;
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
    private string updateStatusText = string.Empty;
    private bool isUpdateStatusVisible;
    private bool isUpdateAvailable;
    private bool isUpdateBusy;

    public MainWindowViewModel(
        IDisplayTimeoutService displayTimeoutService,
        ISleepPreventionService sleepPreventionService,
        IDisplayOffService displayOffService,
        IPowerSourceProvider powerSourceProvider,
        IUserSettingsStore settingsStore,
        IApplicationUpdateService? applicationUpdateService = null)
    {
        this.displayTimeoutService = displayTimeoutService;
        this.sleepPreventionService = sleepPreventionService;
        this.displayOffService = displayOffService;
        this.powerSourceProvider = powerSourceProvider;
        this.settingsStore = settingsStore;
        this.applicationUpdateService = applicationUpdateService;

        applyAcCommand = new(() => ApplyTimeoutAsync(PowerSettingTarget.AcPower), () => !IsBusy);
        applyBatteryCommand = new(() => ApplyTimeoutAsync(PowerSettingTarget.Battery), () => !IsBusy);
        refreshCommand = new(RefreshAsync, () => !IsBusy);
        toggleSleepPreventionCommand = new(ToggleSleepPreventionAsync, () => !IsBusy);
        updateAcPowerOnlyCommand = new(UpdateAcPowerOnlyAsync, () => !IsBusy);
        toggleDisplayOffCountdownCommand = new(
            ToggleDisplayOffCountdownAsync,
            () => displayOffCountdown.IsActive || !IsBusy);
        checkForUpdatesCommand = new(
            () => CheckForUpdatesAsync(showNoUpdateStatus: true),
            () => applicationUpdateService is not null && !IsUpdateBusy);
        applyUpdateCommand = new(
            DownloadUpdateAsync,
            () => applicationUpdateService is not null && IsUpdateAvailable && !IsUpdateBusy);

        powerSourceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        powerSourceTimer.Tick += HandlePowerSourceTimerTick;
        displayOffCountdownTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        displayOffCountdownTimer.Tick += HandleDisplayOffCountdownTimerTick;
    }

    public event EventHandler? DisplayOffRequested;

    public event EventHandler? UpdateReadyToApply;

    public ICommand ApplyAcCommand => applyAcCommand;

    public ICommand ApplyBatteryCommand => applyBatteryCommand;

    public ICommand RefreshCommand => refreshCommand;

    public ICommand ToggleSleepPreventionCommand => toggleSleepPreventionCommand;

    public ICommand UpdateAcPowerOnlyCommand => updateAcPowerOnlyCommand;

    public ICommand ToggleDisplayOffCountdownCommand => toggleDisplayOffCountdownCommand;

    public ICommand CheckForUpdatesCommand => checkForUpdatesCommand;

    public ICommand ApplyUpdateCommand => applyUpdateCommand;

    public string CurrentVersionText => applicationUpdateService?.CurrentVersionText ?? "v?";

    public string UpdateStatusText
    {
        get => updateStatusText;
        private set => SetProperty(ref updateStatusText, value);
    }

    public bool IsUpdateStatusVisible
    {
        get => isUpdateStatusVisible;
        private set => SetProperty(ref isUpdateStatusVisible, value);
    }

    public bool IsUpdateAvailable
    {
        get => isUpdateAvailable;
        private set
        {
            if (SetProperty(ref isUpdateAvailable, value))
            {
                applyUpdateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsUpdateBusy
    {
        get => isUpdateBusy;
        private set
        {
            if (SetProperty(ref isUpdateBusy, value))
            {
                checkForUpdatesCommand.NotifyCanExecuteChanged();
                applyUpdateCommand.NotifyCanExecuteChanged();
            }
        }
    }

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

    public bool HasGeneralError => !string.IsNullOrEmpty(GeneralErrorMessage);

    public bool HasDisplayOffError => !string.IsNullOrEmpty(DisplayOffErrorMessage);

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

    public string GeneralErrorMessage
    {
        get => generalErrorMessage;
        private set
        {
            if (SetProperty(ref generalErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasGeneralError));
            }
        }
    }

    public string DisplayOffErrorMessage
    {
        get => displayOffErrorMessage;
        private set
        {
            if (SetProperty(ref displayOffErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasDisplayOffError));
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

    public bool IsDisplayOffCountdownActive => displayOffCountdown.IsActive;

    public int DisplayOffRemainingSeconds => displayOffCountdown.RemainingSeconds;

    public string DisplayOffButtonText => IsDisplayOffCountdownActive
        ? $"{DisplayOffRemainingSeconds}秒後にオフ　キャンセル"
        : "ディスプレイをオフ";

    public string DisplayOffAutomationName => IsDisplayOffCountdownActive
        ? $"ディスプレイを{DisplayOffRemainingSeconds}秒後にオフ。押すとキャンセルします"
        : "ディスプレイをオフ。3秒のカウントダウンを開始します";

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

    public Task CheckForUpdatesSilentlyAsync() => CheckForUpdatesAsync(showNoUpdateStatus: false);

    public void HandleSystemResume()
    {
        if (!IsSleepPreventionActive)
        {
            return;
        }

        try
        {
            sleepPreventionService.Renew();
            SleepErrorMessage = string.Empty;
        }
        catch (Exception exception)
        {
            IsSleepPreventionRequested = false;
            IsSleepPreventionActive = false;
            SleepPreventionStatusText = "エラーのため解除";
            SleepErrorMessage = FormatError(exception);
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
        displayOffCountdownTimer.Stop();
        displayOffCountdownTimer.Tick -= HandleDisplayOffCountdownTimerTick;
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
            RefreshPowerSource();
            AcErrorMessage = string.Empty;
            BatteryErrorMessage = string.Empty;
        });
    }

    private Task ToggleSleepPreventionAsync()
    {
        SleepErrorMessage = string.Empty;
        IsSleepPreventionRequested = !IsSleepPreventionRequested;
        ApplySleepPreventionDecision();
        return Task.CompletedTask;
    }

    private Task ToggleDisplayOffCountdownAsync()
    {
        if (displayOffCountdown.IsActive)
        {
            CancelDisplayOffCountdown();
        }
        else
        {
            displayOffCountdown.Start();
            displayOffCountdownTimer.Start();
            NotifyDisplayOffCountdownChanged();
        }

        return Task.CompletedTask;
    }

    internal void CancelDisplayOffCountdown()
    {
        if (!displayOffCountdown.IsActive)
        {
            return;
        }

        displayOffCountdownTimer.Stop();
        displayOffCountdown.Cancel();
        NotifyDisplayOffCountdownChanged();
    }

    internal void AdvanceDisplayOffCountdown()
    {
        if (!displayOffCountdown.IsActive)
        {
            return;
        }

        bool shouldTurnOff = displayOffCountdown.Tick();
        NotifyDisplayOffCountdownChanged();
        if (!shouldTurnOff)
        {
            return;
        }

        displayOffCountdownTimer.Stop();
        DisplayOffRequested?.Invoke(this, EventArgs.Empty);
    }

    internal void TurnOffDisplay()
    {
        DisplayOffErrorMessage = string.Empty;
        try
        {
            displayOffService.TurnOff();
        }
        catch (Exception exception)
        {
            DisplayOffErrorMessage = FormatError(exception);
        }
    }

    private async Task CheckForUpdatesAsync(bool showNoUpdateStatus)
    {
        if (applicationUpdateService is null || IsUpdateBusy)
        {
            return;
        }

        IsUpdateBusy = true;
        try
        {
            ApplicationUpdateCheckResult result = await applicationUpdateService.CheckAsync();
            IsUpdateAvailable = result.IsUpdateAvailable;
            if (!result.IsInstalled)
            {
                IsUpdateStatusVisible = showNoUpdateStatus;
                UpdateStatusText = "自動更新はインストール版で利用できます";
            }
            else if (result.IsUpdateAvailable)
            {
                IsUpdateStatusVisible = true;
                UpdateStatusText = $"DisplayLight {result.AvailableVersion}を利用できます";
            }
            else
            {
                IsUpdateStatusVisible = showNoUpdateStatus;
                UpdateStatusText = "DisplayLightは最新版です";
            }
        }
        catch (Exception exception)
        {
            IsUpdateAvailable = false;
            IsUpdateStatusVisible = showNoUpdateStatus;
            UpdateStatusText = showNoUpdateStatus
                ? $"更新を確認できませんでした：{exception.Message}"
                : string.Empty;
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }

    private async Task DownloadUpdateAsync()
    {
        if (applicationUpdateService is null || !IsUpdateAvailable || IsUpdateBusy)
        {
            return;
        }

        IsUpdateBusy = true;
        bool downloadFinished = false;
        try
        {
            Progress<int> progress = new(value =>
            {
                if (!downloadFinished)
                {
                    UpdateStatusText = $"更新をダウンロードしています（{value}%）";
                }
            });
            await applicationUpdateService.DownloadAsync(progress);
            downloadFinished = true;
            UpdateStatusText = "更新を適用して再起動します";
            UpdateReadyToApply?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            downloadFinished = true;
            UpdateStatusText = $"更新をダウンロードできませんでした：{exception.Message}";
        }
        finally
        {
            downloadFinished = true;
            IsUpdateBusy = false;
        }
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

        try
        {
            await operation();
            if (onError is null)
            {
                GeneralErrorMessage = string.Empty;
            }
        }
        catch (Exception exception)
        {
            if (onError is null)
            {
                GeneralErrorMessage = FormatError(exception);
            }
            else
            {
                onError(exception);
            }
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

    private void RefreshPowerSource()
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

            if (changed)
            {
                ApplySleepPreventionDecision();
            }
        }
        catch (Exception)
        {
            currentPowerSource = PowerSource.Unknown;
            PowerSourceText = "電源：取得失敗";
            ApplySleepPreventionDecision();
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
            SleepErrorMessage = string.Empty;
        }
        catch (Exception exception)
        {
            IsSleepPreventionRequested = false;
            IsSleepPreventionActive = false;
            SleepPreventionStatusText = "エラーのため解除";
            SleepErrorMessage = FormatError(exception);
        }
    }

    private void HandlePowerSourceTimerTick(object? sender, EventArgs e) =>
        RefreshPowerSource();

    private void HandleDisplayOffCountdownTimerTick(object? sender, EventArgs e) =>
        AdvanceDisplayOffCountdown();

    private void NotifyDisplayOffCountdownChanged()
    {
        OnPropertyChanged(nameof(IsDisplayOffCountdownActive));
        OnPropertyChanged(nameof(DisplayOffRemainingSeconds));
        OnPropertyChanged(nameof(DisplayOffButtonText));
        OnPropertyChanged(nameof(DisplayOffAutomationName));
        toggleDisplayOffCountdownCommand.NotifyCanExecuteChanged();
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
        toggleDisplayOffCountdownCommand.NotifyCanExecuteChanged();
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

}
