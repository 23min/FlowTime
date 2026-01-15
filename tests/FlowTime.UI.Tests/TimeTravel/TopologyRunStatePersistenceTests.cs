using System;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.UI.Pages.TimeTravel;
using Microsoft.JSInterop;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class TopologyRunStatePersistenceTests
{
    [Fact]
    public async Task ScheduleRunStateSave_DebouncesRapidRequests()
    {
        var js = new RecordingJSRuntime();
        var topology = new Topology();
        topology.TestSetJsRuntime(js);
        topology.TestSetCurrentRunId("run-id");
        topology.TestMarkHasLoaded();
        topology.TestSetRunStateSaveDelay(TimeSpan.FromMilliseconds(1));
        topology.TestOverrideRunStateSaveInvoker(callback => callback());

        topology.TestScheduleRunStateSave();
        topology.TestScheduleRunStateSave();

        await topology.TestAwaitPendingRunStateSaveAsync();

        Assert.Equal(1, js.SetItemCalls);
    }

    [Fact]
    public async Task ScheduleRunStateSave_WritesAfterDelay()
    {
        var js = new RecordingJSRuntime();
        var topology = new Topology();
        topology.TestSetJsRuntime(js);
        topology.TestSetCurrentRunId("run-id");
        topology.TestMarkHasLoaded();
        topology.TestSetRunStateSaveDelay(TimeSpan.FromMilliseconds(1));
        topology.TestOverrideRunStateSaveInvoker(callback => callback());

        topology.TestScheduleRunStateSave();
        await topology.TestAwaitPendingRunStateSaveAsync();

        Assert.Equal(1, js.SetItemCalls);
        Assert.Equal("ft.topology.state.v2:run-id", js.LastKey);
        Assert.False(string.IsNullOrWhiteSpace(js.LastPayload));
    }

    private sealed class RecordingJSRuntime : IJSRuntime
    {
        public int SetItemCalls { get; private set; }
        public string? LastKey { get; private set; }
        public string? LastPayload { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (string.Equals(identifier, "localStorage.setItem", StringComparison.Ordinal))
            {
                SetItemCalls++;
                LastKey = args is { Length: > 0 } ? args[0] as string : null;
                LastPayload = args is { Length: > 1 } ? args[1] as string : null;
                return new ValueTask<TValue>(default(TValue)!);
            }

            if (string.Equals(identifier, "localStorage.getItem", StringComparison.Ordinal))
            {
                return new ValueTask<TValue>(default(TValue)!);
            }

            throw new NotSupportedException($"JS identifier '{identifier}' is not supported in tests.");
        }
    }
}
