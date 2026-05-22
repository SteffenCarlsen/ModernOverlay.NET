using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
namespace ModernOverlay.Win32;

internal sealed class Win32OwnerThread : IDisposable
{
    private readonly AutoResetEvent workAvailable = new(false);
    private readonly ConcurrentQueue<WorkItem> workItems = new();
    private readonly TaskCompletionSource<object?> initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread thread;
    private bool disposed;
    private bool stopRequested;
    private int ownerThreadId;

    public Win32OwnerThread()
    {
        thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "ModernOverlay Win32 owner",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        initialized.Task.GetAwaiter().GetResult();
    }

    public int OwnerThreadId => ownerThreadId;

    public T Invoke<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfDisposed();

        if (Environment.CurrentManagedThreadId == ownerThreadId)
        {
            return action();
        }

        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        workItems.Enqueue(new WorkItem(() => action(), completion));
        workAvailable.Set();
        return (T)completion.Task.GetAwaiter().GetResult()!;
    }

    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Invoke<object?>(() =>
        {
            action();
            return null;
        });
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (Environment.CurrentManagedThreadId == ownerThreadId)
        {
            stopRequested = true;
            workAvailable.Set();
            return;
        }

        if (initialized.Task.IsCompletedSuccessfully)
        {
            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            workItems.Enqueue(new WorkItem(() =>
            {
                stopRequested = true;
                return null;
            }, completion));
            workAvailable.Set();
            completion.Task.GetAwaiter().GetResult();
        }

        thread.Join();
        workAvailable.Dispose();
    }

    private void ThreadMain()
    {
        ownerThreadId = Environment.CurrentManagedThreadId;
        bool comInitialized = false;

        try
        {
            int hr = NativeMethods.CoInitializeEx(0, NativeMethods.CoinitApartmentThreaded);
            if (hr is NativeMethods.SOk or NativeMethods.SFalse)
            {
                comInitialized = true;
            }
            else if (hr != NativeMethods.RpcEChangedMode)
            {
                throw new InvalidOperationException($"CoInitializeEx failed with HRESULT 0x{hr:X8}.");
            }

            initialized.SetResult(null);
            RunMessageLoop();
        }
        catch (Exception ex)
        {
            initialized.TrySetException(ex);
            FailQueuedWork(ex);
        }
        finally
        {
            if (comInitialized)
            {
                NativeMethods.CoUninitialize();
            }
        }
    }

    private void RunMessageLoop()
    {
        nint[] handles = [workAvailable.SafeWaitHandle.DangerousGetHandle()];

        while (!stopRequested)
        {
            DrainWorkItems();
            DrainMessages();

            if (stopRequested)
            {
                break;
            }

            _ = NativeMethods.MsgWaitForMultipleObjectsEx(
                1,
                handles,
                NativeMethods.Infinite,
                NativeMethods.QsAllInput,
                NativeMethods.MwmoInputAvailable);
        }

        DrainWorkItems();
        DrainMessages();
    }

    private void DrainMessages()
    {
        while (NativeMethods.PeekMessage(out NativeMethods.Msg message, 0, 0, 0, NativeMethods.PmRemove))
        {
            _ = NativeMethods.TranslateMessage(message);
            _ = NativeMethods.DispatchMessage(message);
        }
    }

    private void DrainWorkItems()
    {
        while (workItems.TryDequeue(out WorkItem? item))
        {
            try
            {
                object? result = item.Action();
                item.Completion.SetResult(result);
            }
            catch (Exception ex)
            {
                item.Completion.SetException(ex);
            }
        }
    }

    private void FailQueuedWork(Exception exception)
    {
        while (workItems.TryDequeue(out WorkItem? item))
        {
            item.Completion.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(exception));
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed record WorkItem(Func<object?> Action, TaskCompletionSource<object?> Completion);
}
