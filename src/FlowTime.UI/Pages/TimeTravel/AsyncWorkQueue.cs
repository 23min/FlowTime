using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlowTime.UI.Pages.TimeTravel;

/// <summary>
/// Generic async work queue that executes the latest scheduled job on a background thread and
/// forwards results back to the caller.
/// </summary>
/// <typeparam name="TJob">Payload that describes the work to execute.</typeparam>
/// <typeparam name="TResult">Result produced by the work delegate.</typeparam>
internal sealed class AsyncWorkQueue<TJob, TResult> : IDisposable
{
    private readonly Func<TJob, CancellationToken, TResult> workFunc;
    private readonly Func<TResult, Task> resultHandler;
    private readonly object gate = new();

    private CancellationTokenSource? currentCts;
    private Task? currentTask;
    private bool hasPendingJob;
    private TJob? pendingJob;
    private bool disposed;

    public AsyncWorkQueue(
        Func<TJob, CancellationToken, TResult> workFunc,
        Func<TResult, Task> resultHandler)
    {
        this.workFunc = workFunc ?? throw new ArgumentNullException(nameof(workFunc));
        this.resultHandler = resultHandler ?? throw new ArgumentNullException(nameof(resultHandler));
    }

    public void Schedule(TJob job, bool cancelCurrent)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(AsyncWorkQueue<TJob, TResult>));
        }

        lock (gate)
        {
            pendingJob = job;
            hasPendingJob = true;

            if (currentTask is null)
            {
                StartNextLocked();
                return;
            }

            if (cancelCurrent)
            {
                currentCts?.Cancel();
            }
        }
    }

    public void Cancel(bool clearQueue)
    {
        lock (gate)
        {
            if (clearQueue)
            {
                hasPendingJob = false;
                pendingJob = default;
            }

            currentCts?.Cancel();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Cancel(clearQueue: true);
    }

    private void StartNextLocked()
    {
        if (!hasPendingJob)
        {
            return;
        }

        var job = pendingJob!;
        hasPendingJob = false;

        var localCts = new CancellationTokenSource();
        currentCts = localCts;

        currentTask = ProcessJobAsync(job, localCts);
    }

    private async Task ProcessJobAsync(TJob job, CancellationTokenSource activeCts)
    {
        try
        {
            while (true)
            {
                activeCts.Token.ThrowIfCancellationRequested();
                var result = await Task.Run(() => workFunc(job, activeCts.Token), activeCts.Token).ConfigureAwait(false);
                await resultHandler(result).ConfigureAwait(false);

                lock (gate)
                {
                    if (!hasPendingJob)
                    {
                        CleanupLocked(activeCts);
                        break;
                    }

                    job = pendingJob!;
                    hasPendingJob = false;
                }
            }
        }
        catch (OperationCanceledException)
        {
            lock (gate)
            {
                CleanupLocked(activeCts);
                if (hasPendingJob && !disposed)
                {
                    StartNextLocked();
                }
            }
        }
        finally
        {
            activeCts.Dispose();
        }
    }

    private void CleanupLocked(CancellationTokenSource activeCts)
    {
        currentTask = null;
        if (ReferenceEquals(currentCts, activeCts))
        {
            currentCts = null;
        }
    }
}
