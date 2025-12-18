using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.UI.Pages.TimeTravel;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class AsyncWorkQueueTests
{
    [Fact]
    public async Task Schedule_CancelsInFlightWork_WhenRequested()
    {
        var completed = new TaskCompletionSource<string>();
        var startedA = new TaskCompletionSource();
        var startedB = new TaskCompletionSource();
        var results = new List<string>();

        using var queue = new AsyncWorkQueue<string, string>(
            (job, token) =>
            {
                if (job == "A")
                {
                    startedA.TrySetResult();
                    // Wait indefinitely until cancelled.
                    Task.Delay(Timeout.Infinite, token).GetAwaiter().GetResult();
                }
                else
                {
                    startedB.TrySetResult();
                }

                token.ThrowIfCancellationRequested();
                return job;
            },
            result =>
            {
                results.Add(result);
                completed.TrySetResult(result);
                return Task.CompletedTask;
            });

        queue.Schedule("A", cancelCurrent: true);
        await startedA.Task.WaitAsync(TimeSpan.FromSeconds(2));

        queue.Schedule("B", cancelCurrent: true);
        await startedB.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var winner = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("B", winner);
        Assert.Equal(new[] { "B" }, results);
    }

    [Fact]
    public async Task Schedule_QueuesWork_WhenNotCancelled()
    {
        var order = new List<string>();
        var completion = new TaskCompletionSource();

        using var queue = new AsyncWorkQueue<string, string>(
            (job, token) =>
            {
                if (job == "A")
                {
                    Thread.Sleep(50);
                }

                return job;
            },
            result =>
            {
                order.Add(result);
                if (order.Count == 2)
                {
                    completion.TrySetResult();
                }

                return Task.CompletedTask;
            });

        queue.Schedule("A", cancelCurrent: true);
        queue.Schedule("B", cancelCurrent: false);

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "A", "B" }, order);
    }
}
