using System.ComponentModel;
using System.Runtime.InteropServices;
using DisplayLight.Core.Abstractions;

namespace DisplayLight.App.Infrastructure.Windows;

internal sealed class WindowsSleepPreventionService : ISleepPreventionService
{
    private const uint PowerRequestContextVersion = 0;
    private const uint PowerRequestContextSimpleString = 1;
    private const string RequestReason = "DisplayLightで手動の作業が進行中です。";

    private readonly object syncRoot = new();
    private SafePowerRequestHandle? handle;
    private bool isDisposed;

    public bool IsActive
    {
        get
        {
            lock (syncRoot)
            {
                return handle is not null;
            }
        }
    }

    public void SetActive(bool isActive)
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (isActive == (handle is not null))
            {
                return;
            }

            if (isActive)
            {
                Acquire();
            }
            else
            {
                Release();
            }
        }
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (isDisposed)
            {
                return;
            }

            try
            {
                Release();
            }
            finally
            {
                isDisposed = true;
            }
        }
    }

    public void Renew()
    {
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (handle is null)
            {
                return;
            }

            handle.Dispose();
            handle = null;
            Acquire();
        }
    }

    private void Acquire()
    {
        nint reasonPointer = Marshal.StringToHGlobalUni(RequestReason);

        try
        {
            PowerRequestReasonContext context = new(
                PowerRequestContextVersion,
                PowerRequestContextSimpleString,
                reasonPointer);
            SafePowerRequestHandle newHandle = new(PowerRequestNativeMethods.PowerCreateRequest(in context));

            if (newHandle.IsInvalid)
            {
                int errorCode = Marshal.GetLastPInvokeError();
                newHandle.Dispose();
                throw new Win32Exception(errorCode, "スリープ防止要求を作成できませんでした。");
            }

            if (!PowerRequestNativeMethods.PowerSetRequest(newHandle, PowerRequestType.SystemRequired))
            {
                int errorCode = Marshal.GetLastPInvokeError();
                newHandle.Dispose();
                throw new Win32Exception(errorCode, "システムスリープ防止を有効にできませんでした。");
            }

            handle = newHandle;
        }
        finally
        {
            Marshal.FreeHGlobal(reasonPointer);
        }
    }

    private void Release()
    {
        SafePowerRequestHandle? currentHandle = handle;
        if (currentHandle is null)
        {
            return;
        }

        handle = null;

        try
        {
            if (!PowerRequestNativeMethods.PowerClearRequest(currentHandle, PowerRequestType.SystemRequired))
            {
                int errorCode = Marshal.GetLastPInvokeError();
                throw new Win32Exception(errorCode, "システムスリープ防止を解除できませんでした。");
            }
        }
        finally
        {
            currentHandle.Dispose();
        }
    }
}
