using FlowTime.Core;
using FlowTime.Core.Compiler;
using FlowTime.Core.Models;
using FlowTime.Core.TimeTravel;

namespace FlowTime.Core.Tests.TimeTravel;

public sealed class ModeValidatorTests
{
    private static readonly Window testWindow = new()
    {
        Bins = 4,
        BinSize = 5,
        BinUnit = TimeUnit.Minutes,
        StartTime = DateTime.UtcNow
    };

    private static readonly Topology testTopology = new()
    {
        Nodes = new List<Node>
        {
            new()
            {
                Id = "Service",
                Kind = "service",
                Analytical = new RuntimeAnalyticalDescriptor
                {
                    Identity = RuntimeAnalyticalIdentity.Service,
                    Category = RuntimeAnalyticalNodeCategory.Service,
                    HasQueueSemantics = false,
                    HasServiceSemantics = true,
                    HasCycleTimeDecomposition = false,
                    StationarityWarningApplicable = false
                },
                Semantics = new NodeSemantics
                {
                    Arrivals = Ref("arrivals"),
                    Served = Ref("served"),
                    Errors = Ref("errors"),
                    Capacity = Ref("capacity")
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
            testWindow,
            testTopology,
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
            testWindow,
            testTopology,
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

    [Fact]
    public void Validate_SimulationQueueBackedServiceMissingQueueDepth_ReturnsError()
    {
        var topology = new Topology
        {
            Nodes = new List<Node>
            {
                new()
                {
                    Id = "BufferedService",
                    Kind = "service",
                    Analytical = new RuntimeAnalyticalDescriptor
                    {
                        Identity = RuntimeAnalyticalIdentity.ServiceWithBuffer,
                        Category = RuntimeAnalyticalNodeCategory.Service,
                        HasQueueSemantics = true,
                        HasServiceSemantics = true,
                        HasCycleTimeDecomposition = true,
                        StationarityWarningApplicable = true,
                        QueueSourceNodeId = "queue_helper"
                    },
                    Semantics = new NodeSemantics
                    {
                        Arrivals = Ref("arrivals"),
                        Served = Ref("served"),
                        QueueDepth = Ref("queue_helper")
                    }
                }
            },
            Edges = new List<Edge>()
        };

        var nodeData = new Dictionary<string, NodeData>(StringComparer.Ordinal)
        {
            ["BufferedService"] = new NodeData
            {
                NodeId = "BufferedService",
                Arrivals = new[] { 1.0, 1.0, 1.0, 1.0 },
                Served = new[] { 1.0, 1.0, 1.0, 1.0 },
                QueueDepth = Array.Empty<double>()
            }
        };

        var context = new ModeValidationContext(
            CreateMetadata(mode: "simulation"),
            testWindow,
            topology,
            nodeData,
            Array.Empty<ModeValidationWarning>(),
            new Dictionary<string, IReadOnlyList<ModeValidationWarning>>());
        var validator = new ModeValidator();

        var result = validator.Validate(context);

        Assert.True(result.HasErrors);
        Assert.Equal("mode_validation_failed", result.ErrorCode);
        Assert.Contains("queue", result.ErrorMessage);
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

    private static CompiledSeriesReference Ref(string value) =>
        SemanticReferenceResolver.ParseSeriesReference(value);
}
