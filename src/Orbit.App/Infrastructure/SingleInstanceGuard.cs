using System.Threading;

namespace Orbit.App.Infrastructure;

/// <summary>
/// Ensures only one Orbit runs per user session. A second launch signals the
/// first instance (which brings its window back) and then exits.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\Orbit.SingleInstance.v1";
    private const string EventName = @"Local\Orbit.Activate.v1";

    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;
    private Thread? _listener;
    private volatile bool _running;

    /// <summary>Raised on the UI thread's behalf when another instance asks us to come forward.</summary>
    public event Action? ActivationRequested;

    public bool IsPrimary { get; private set; }

    /// <summary>Returns true if this is the first instance. If false, call
    /// <see cref="SignalPrimary"/> and exit.</summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsPrimary = createdNew;

        if (createdNew)
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            _running = true;
            _listener = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "Orbit.ActivateListener"
            };
            _listener.Start();
        }

        return createdNew;
    }

    /// <summary>Called by a secondary instance to wake the primary one.</summary>
    public static void SignalPrimary()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(EventName, out var handle))
            {
                handle.Set();
                handle.Dispose();
            }
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Primary is gone already – nothing to signal.
        }
    }

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                if (_activateEvent!.WaitOne(750))
                    ActivationRequested?.Invoke();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _activateEvent?.Set();   // unblock the listener so it can exit
        _activateEvent?.Dispose();

        try { _mutex?.ReleaseMutex(); }
        catch (ApplicationException) { /* not owned */ }
        _mutex?.Dispose();
    }
}
