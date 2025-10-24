using FlowTime.UI.Pages.TimeTravel;

namespace FlowTime.UI.Tests;

public sealed class RunSubmissionSnapshotStorageTests
{
    [Fact]
    public void Snapshot_RoundTripsThroughJson()
    {
        var snapshot = new RunSubmissionSnapshot(
            TemplateId: "order-system",
            Mode: OrchestrationMode.Telemetry,
            SubmittedAtUtc: DateTimeOffset.Parse("2025-10-20T16:05:00Z"),
            IsDryRun: false)
        {
            CaptureDirectory = "/captures/batch-42",
            ParameterText = "{\"foo\":1}",
            TelemetryBindingsText = "pressure=node-1",
            RngSeedText = "456"
        };

        var json = RunSubmissionSnapshotStorage.Serialize(snapshot);
        Assert.Contains("\"templateId\":\"order-system\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"telemetry\"", json, StringComparison.OrdinalIgnoreCase);

        var roundTrip = RunSubmissionSnapshotStorage.TryDeserialize(json);
        Assert.NotNull(roundTrip);
        Assert.Equal(snapshot.TemplateId, roundTrip!.TemplateId);
        Assert.Equal(snapshot.Mode, roundTrip.Mode);
        Assert.Equal(snapshot.SubmittedAtUtc, roundTrip.SubmittedAtUtc);
        Assert.Equal(snapshot.CaptureDirectory, roundTrip.CaptureDirectory);
        Assert.Equal(snapshot.TelemetryBindingsText, roundTrip.TelemetryBindingsText);
        Assert.Equal(snapshot.RngSeedText, roundTrip.RngSeedText);
        Assert.False(roundTrip.IsDryRun);
    }

    [Fact]
    public void Deserialize_InvalidPayload_ReturnsNull()
    {
        var roundTrip = RunSubmissionSnapshotStorage.TryDeserialize("{\"invalid\":true}");
        Assert.Null(roundTrip);
    }
}

public sealed class RunSuccessSnapshotStorageTests
{
    [Fact]
    public void Snapshot_RoundTripsThroughJson()
    {
        var snapshot = new RunSuccessSnapshot(
            RunId: "run-123",
            TemplateId: "order-system",
            TemplateTitle: "Order System",
            TemplateVersion: "1.2.3",
            Mode: "telemetry",
            TelemetryResolved: true,
            CompletedAtUtc: DateTimeOffset.Parse("2025-10-20T16:30:00Z"),
            Warnings: new[]
            {
                new RunSuccessWarning("WARN-1", "Example warning", "node-1")
            },
            RngSeed: 789);

        var json = RunSuccessSnapshotStorage.Serialize(snapshot);
        Assert.Contains("\"runId\":\"run-123\"", json, StringComparison.Ordinal);

        var roundTrip = RunSuccessSnapshotStorage.TryDeserialize(json);
        Assert.NotNull(roundTrip);
        Assert.Equal(snapshot.RunId, roundTrip!.RunId);
        Assert.Equal("Order System", roundTrip.TemplateTitle);
        Assert.True(roundTrip.TelemetryResolved);
        Assert.Single(roundTrip.Warnings);
        Assert.Equal("WARN-1", roundTrip.Warnings[0].Code);
        Assert.Equal(789, roundTrip.RngSeed);
    }

    [Fact]
    public void Deserialize_InvalidPayload_ReturnsNull()
    {
        var roundTrip = RunSuccessSnapshotStorage.TryDeserialize("{\"runId\":null}");
        Assert.Null(roundTrip);
    }
}
