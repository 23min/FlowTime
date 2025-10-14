using FlowTime.Core;
using FlowTime.Core.Models;
using FlowTime.Core.TimeTravel;

namespace FlowTime.Core.Tests.TimeTravel;

public sealed class ModeValidatorTests
{
    private static readonly Window TestWindow = new()
    {
        Bins = 4,
        BinSize = 5,
        BinUnit = TimeUnit.Minutes,
        StartTime = DateTime.UtcNow
    };

    private static readonly Topology TestTopology = new()
    {
        Nodes = new List<Node>
        {
            new()
            {
                Id = "Service",
                Kind = "service",
                Semantics = new NodeSemantics
                {
                    Arrivals = "arrivals",
                    Served = "served",
                    Errors = "errors",
                    Capacity = "capacity"
                }
            }
        },
        Edges = new List<Edge>()
    };

    [Fact]
    public void Validate_SimulationMissingArrivals_ReturnsError()
    {
        var nodeData = new Dictionary<string, NodeData>(StringComparer.Ordinal)
        {
            ["Service"] = new NodeData
            {
                NodeId = "Service",
                Arrivals = Array.Empty<double>(),
                Served = new[] { 1.0, 2.0, 3.0, 4.0 },
                Errors = new[] { 0.0, 0.0, 0.0, 0.0 },
                Capacity = new[] { 5.0, 5.0, 5.0, 5.0 }
            }
        };

        var manifestMetadata = CreateMetadata(mode: "simulation");

        var context = new ModeValidationContext(
            manifestMetadata,
            TestWindow,
            TestTopology,
            nodeData,
            Array.Empty<ModeValidationWarning>(),
            new Dictionary<string, IReadOnlyList<ModeValidationWarning>>());
        var validator = new ModeValidator();

        var result = validator.Validate(context);

        Assert.True(result.HasErrors);
        Assert.Equal("mode_validation_failed", result.ErrorCode);
        Assert.Contains("missing required", result.ErrorMessage);
    }

    [Fact]
    public void Validate_TelemetryMissingSources_ProducesWarnings()
    {
        var nodeData = new Dictionary<string, NodeData>(StringComparer.Ordinal)
        {
            ["Service"] = new NodeData
            {
                NodeId = "Service",
                Arrivals = new[] { 1.0, 2.0, 3.0, 4.0 },
                Served = new[] { 1.0, 2.0, 3.0, 4.0 },
                Errors = new[] { 0.0, 0.0, 0.0, 0.0 },
                Capacity = new[] { 5.0, 5.0, 5.0, 5.0 }
            }
        };

        var manifestMetadata = CreateMetadata(mode: "telemetry");

        var context = new ModeValidationContext(
            manifestMetadata,
            TestWindow,
            TestTopology,
            nodeData,
            Array.Empty<ModeValidationWarning>(),
            new Dictionary<string, IReadOnlyList<ModeValidationWarning>>());
        var validator = new ModeValidator();

        var result = validator.Validate(context);

        Assert.False(result.HasErrors);
        Assert.Contains(result.Warnings, w => w.Code == "telemetry_sources_missing");
        Assert.True(result.NodeWarnings.ContainsKey("Service"));
        Assert.Contains(result.NodeWarnings["Service"], w => w.Code == "telemetry_sources_unresolved");
    }

    private static RunManifestMetadata CreateMetadata(string mode)
    {
        return new RunManifestMetadata
        {
            TemplateId = "template",
            TemplateTitle = "Template",
            TemplateVersion = "1.0.0",
            Mode = mode,
            Schema = new RunSchemaMetadata
            {
                Id = "time-travel/v1",
                Version = "1",
                Hash = "sha256:abc"
            },
            ProvenanceHash = "sha256:abc",
            Storage = new RunStorageDescriptor
            {
                ModelPath = "/tmp/model.yaml"
            }
        };
    }
}
