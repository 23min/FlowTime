using FlowTime.UI.Pages.TimeTravel;

namespace FlowTime.UI.Tests;

public sealed class RunOrchestrationStateMachineTests
{
    [Fact]
    public void PreventsConcurrentSubmissions()
    {
        var machine = new RunOrchestrationStateMachine();
        var snapshot = new RunSubmissionSnapshot("order-system", OrchestrationMode.Telemetry, SubmittedAtUtc: DateTimeOffset.Parse("2025-10-20T16:00:00Z"), IsDryRun: false);

        Assert.Equal(RunOrchestrationPhase.Idle, machine.Phase);

        var first = machine.TryBeginPlanning(snapshot);
        Assert.True(first);
        Assert.Equal(RunOrchestrationPhase.Planning, machine.Phase);

        var second = machine.TryBeginPlanning(snapshot);
        Assert.False(second); // still planning → block duplicate submission
        Assert.Equal(RunOrchestrationPhase.Planning, machine.Phase);

        machine.PromoteToRunning();
        Assert.Equal(RunOrchestrationPhase.Running, machine.Phase);

        machine.CompleteSuccess(new RunOrchestrationSuccess("run-42"));
        Assert.Equal(RunOrchestrationPhase.Success, machine.Phase);
        Assert.NotNull(machine.LastSuccess);
        Assert.Equal("run-42", machine.LastSuccess!.RunId);

        var third = machine.TryBeginPlanning(snapshot with { SubmittedAtUtc = snapshot.SubmittedAtUtc.AddMinutes(5) });
        Assert.True(third);
    }

    [Fact]
    public void CompleteFailure_PersistsError()
    {
        var machine = new RunOrchestrationStateMachine();
        var snapshot = new RunSubmissionSnapshot("order-system", OrchestrationMode.Simulation, DateTimeOffset.Parse("2025-10-20T16:00:00Z"), IsDryRun: true);

        machine.TryBeginPlanning(snapshot);
        machine.PromoteToRunning();
        machine.CompleteFailure("HTTP 500");

        Assert.Equal(RunOrchestrationPhase.Failure, machine.Phase);
        Assert.Equal("HTTP 500", machine.LastError);
        Assert.Null(machine.LastSuccess);

        var retry = machine.TryBeginPlanning(snapshot with { SubmittedAtUtc = snapshot.SubmittedAtUtc.AddMinutes(1) });
        Assert.True(retry);
    }
}
