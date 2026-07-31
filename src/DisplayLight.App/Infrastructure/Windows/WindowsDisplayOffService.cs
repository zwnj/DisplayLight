using System.IO;
using DisplayLight.Core.Abstractions;
using DisplayLight.Core.Power;

namespace DisplayLight.App.Infrastructure.Windows;

internal sealed class WindowsDisplayOffService : IDisplayOffService, IDisposable
{
    internal const uint TemporaryDisplayTimeoutSeconds = 1;
    internal static readonly TimeSpan RestoreDelay = TimeSpan.FromSeconds(5);

    private readonly IPowerSchemeNativeApi nativeApi;
    private readonly IPowerSourceProvider powerSourceProvider;
    private readonly IDisplayOffRecoveryStore recoveryStore;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly CancellationTokenSource shutdownCancellation = new();
    private readonly object syncRoot = new();
    private TemporaryDisplayTimeoutLease? activeLease;
    private bool isOperationActive;
    private bool isDisposed;

    public WindowsDisplayOffService(
        IPowerSchemeNativeApi nativeApi,
        IPowerSourceProvider powerSourceProvider)
        : this(nativeApi, powerSourceProvider, new FileDisplayOffRecoveryStore(), Task.Delay)
    {
    }

    internal WindowsDisplayOffService(
        IPowerSchemeNativeApi nativeApi,
        IPowerSourceProvider powerSourceProvider,
        IDisplayOffRecoveryStore recoveryStore,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        this.nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        this.powerSourceProvider = powerSourceProvider ?? throw new ArgumentNullException(nameof(powerSourceProvider));
        this.recoveryStore = recoveryStore ?? throw new ArgumentNullException(nameof(recoveryStore));
        this.delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
    }

    public void RecoverPendingChange()
    {
        DisplayOffRecoveryState? state = recoveryStore.Load();
        if (state is null)
        {
            return;
        }

        if (state.TemporarySeconds != TemporaryDisplayTimeoutSeconds)
        {
            throw new InvalidDataException(
                "手動消灯の復旧情報に対応していない一時設定が含まれています。Windowsのディスプレイ消灯設定を確認してください。");
        }

        Restore(new TemporaryDisplayTimeoutLease(
            state.SchemeGuid,
            state.Target,
            state.OriginalSeconds));
        recoveryStore.Delete();
    }

    public async Task TurnOffAsync(CancellationToken cancellationToken = default)
    {
        BeginOperation();
        try
        {
            PowerSettingTarget target = GetCurrentTarget();
            Guid schemeGuid = nativeApi.GetActiveScheme();
            uint originalSeconds = ReadDisplayValue(schemeGuid, target);

            if (originalSeconds == TemporaryDisplayTimeoutSeconds)
            {
                return;
            }

            TemporaryDisplayTimeoutLease lease = new(schemeGuid, target, originalSeconds);
            recoveryStore.Save(new DisplayOffRecoveryState(
                schemeGuid,
                target,
                originalSeconds,
                TemporaryDisplayTimeoutSeconds,
                DateTimeOffset.UtcNow));
            SetActiveLease(lease);

            try
            {
                WriteDisplayValue(schemeGuid, target, TemporaryDisplayTimeoutSeconds);
                nativeApi.Activate(schemeGuid);
                using CancellationTokenSource linkedCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        shutdownCancellation.Token);
                await delayAsync(RestoreDelay, linkedCancellation.Token);
            }
            finally
            {
                RestoreActiveLease();
            }
        }
        finally
        {
            EndOperation();
        }
    }

    public void Dispose()
    {
        TemporaryDisplayTimeoutLease? lease;
        lock (syncRoot)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            lease = activeLease;
            activeLease = null;
        }

        shutdownCancellation.Cancel();
        if (lease is not null)
        {
            RestoreAndClearRecovery(lease);
        }
    }

    private void BeginOperation()
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (isOperationActive)
            {
                throw new InvalidOperationException("ディスプレイ消灯処理はすでに実行中です。");
            }

            isOperationActive = true;
        }
    }

    private void EndOperation()
    {
        lock (syncRoot)
        {
            isOperationActive = false;
        }
    }

    private PowerSettingTarget GetCurrentTarget() => powerSourceProvider.GetCurrent() switch
    {
        PowerSource.AcPower => PowerSettingTarget.AcPower,
        PowerSource.Battery => PowerSettingTarget.Battery,
        _ => throw new InvalidOperationException(
            "現在の電源状態を確認できないため、ディスプレイをオフにできませんでした。"),
    };

    private uint ReadDisplayValue(Guid schemeGuid, PowerSettingTarget target) => target switch
    {
        PowerSettingTarget.AcPower => nativeApi.ReadAcDisplayValue(schemeGuid),
        PowerSettingTarget.Battery => nativeApi.ReadDcDisplayValue(schemeGuid),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported power setting target."),
    };

    private void WriteDisplayValue(Guid schemeGuid, PowerSettingTarget target, uint seconds)
    {
        switch (target)
        {
            case PowerSettingTarget.AcPower:
                nativeApi.WriteAcDisplayValue(schemeGuid, seconds);
                break;
            case PowerSettingTarget.Battery:
                nativeApi.WriteDcDisplayValue(schemeGuid, seconds);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported power setting target.");
        }
    }

    private void SetActiveLease(TemporaryDisplayTimeoutLease lease)
    {
        lock (syncRoot)
        {
            activeLease = lease;
        }
    }

    private void RestoreActiveLease()
    {
        TemporaryDisplayTimeoutLease? lease;
        lock (syncRoot)
        {
            lease = activeLease;
            activeLease = null;
        }

        if (lease is not null)
        {
            RestoreAndClearRecovery(lease);
        }
    }

    private void RestoreAndClearRecovery(TemporaryDisplayTimeoutLease lease)
    {
        Restore(lease);
        recoveryStore.Delete();
    }

    private void Restore(TemporaryDisplayTimeoutLease lease)
    {
        uint currentSeconds = ReadDisplayValue(lease.SchemeGuid, lease.Target);
        if (currentSeconds != TemporaryDisplayTimeoutSeconds)
        {
            // The user or another application changed the same setting while the lease
            // was active. Preserve that newer value instead of overwriting it.
            return;
        }

        WriteDisplayValue(lease.SchemeGuid, lease.Target, lease.OriginalSeconds);
        if (nativeApi.GetActiveScheme() == lease.SchemeGuid)
        {
            nativeApi.Activate(lease.SchemeGuid);
        }

        uint restoredSeconds = ReadDisplayValue(lease.SchemeGuid, lease.Target);
        if (restoredSeconds != lease.OriginalSeconds)
        {
            throw new InvalidOperationException(
                "ディスプレイの自動画面オフ設定を変更前の値へ復元できませんでした。Windowsの電源設定を確認してください。");
        }
    }

    private sealed record TemporaryDisplayTimeoutLease(
        Guid SchemeGuid,
        PowerSettingTarget Target,
        uint OriginalSeconds);
}
