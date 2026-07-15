using System.Windows.Threading;

namespace DisplayLight.App.Infrastructure.SingleInstance;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = "DisplayLight.SingleInstance.Mutex";
    private const string ShowEventName = "DisplayLight.SingleInstance.Show";

    private readonly EventWaitHandle showEvent;
    private readonly Mutex sentinelMutex;
    private readonly RegisteredWaitHandle? registeredWait;
    private bool isDisposed;

    public SingleInstanceCoordinator(Dispatcher dispatcher, Action showExistingWindow)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(showExistingWindow);

        NamedWaitHandleOptions options = new()
        {
            CurrentSessionOnly = true,
            CurrentUserOnly = true,
        };

        showEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ShowEventName,
            options,
            out _);
        sentinelMutex = new Mutex(
            initiallyOwned: false,
            MutexName,
            options,
            out bool createdNew);
        IsPrimaryInstance = createdNew;

        if (IsPrimaryInstance)
        {
            registeredWait = ThreadPool.RegisterWaitForSingleObject(
                showEvent,
                (_, _) => dispatcher.BeginInvoke(showExistingWindow),
                state: null,
                Timeout.Infinite,
                executeOnlyOnce: false);
        }
    }

    public bool IsPrimaryInstance { get; }

    public void NotifyPrimaryInstance()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        _ = showEvent.Set();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        registeredWait?.Unregister(waitObject: null);
        showEvent.Dispose();
        sentinelMutex.Dispose();
    }
}
