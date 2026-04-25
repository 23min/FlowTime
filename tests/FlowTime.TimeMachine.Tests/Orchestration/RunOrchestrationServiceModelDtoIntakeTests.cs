// m-E24-02 — Engine intake reads ModelDto.
//
// Pins the contract: RunOrchestrationService validates and builds telemetry
// manifests directly off ModelDto (the unified post-substitution type). The
// pre-unification path deserialized YAML to a Sim-side artifact and read
// leaked-state fields (window.start / window.timezone). m-E24-02 collapses
// that to a single ModelDto-based intake and reads grid.start instead.
//
// All three helpers under test (ValidateSimulationModel /
// BuildSimulationPlanManifest / BuildSimulationTelemetryManifest) are
// internal static methods on RunOrchestrationService — exposed via
// InternalsVisibleTo on the project file. Their public surface is
// CreateRunAsync, but the integration tests only prove the happy path; we
// still owe direct tests on the new branches and on the leaked-state
// absorption guarantee that this refactor depends on.

using FlowTime.Contracts.Dtos;
using FlowTime.TimeMachine.Models;
using FlowTime.TimeMachine.Orchestration;
using FlowTime.Sim.Core.Templates.Exceptions;

namespace FlowTime.TimeMachine.Tests.Orchestration;

public class RunOrchestrationServiceModelDtoIntakeTests
{
    private const string TemplateId = "step3-test";

    // ---------- ValidateSimulationModel ----------

    [Fact]
    public void ValidateSimulationModel_ThrowsWhenGridStartMissing()
    {
        var dto = ValidModel();
        dto.Grid.Start = null;

        var ex = Assert.Throws<TemplateValidationException>(
            () => RunOrchestrationService.ValidateSimulationModel(dto, TemplateId));
        Assert.Contains("grid.start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSimulationModel_ThrowsWhenGridStartEmpty()
    {
        var dto = ValidModel();
        dto.Grid.Start = "   ";

        var ex = Assert.Throws<TemplateValidationException>(
            () => RunOrchestrationService.ValidateSimulationModel(dto, TemplateId));
        Assert.Contains("grid.start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSimulationModel_ThrowsWhenGridBinsZero()
    {
        var dto = ValidModel();
        dto.Grid.Bins = 0;

        var ex = Assert.Throws<TemplateValidationException>(
            () => RunOrchestrationService.ValidateSimulationModel(dto, TemplateId));
        Assert.Contains("grid.bins", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSimulationModel_ThrowsWhenGridBinSizeZero()
    {
        var dto = ValidModel();
        dto.Grid.BinSize = 0;

        var ex = Assert.Throws<TemplateValidationException>(
            () => RunOrchestrationService.ValidateSimulationModel(dto, TemplateId));
        Assert.Contains("grid.binSize", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSimulationModel_ThrowsWhenTopologyMissing()
    {
        var dto = ValidModel();
        dto.Topology = null;

        var ex = Assert.Throws<TemplateValidationException>(
            () => RunOrchestrationService.ValidateSimulationModel(dto, TemplateId));
        Assert.Contains("topology", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSimulationModel_ThrowsWhenTopologyNodesEmpty()
    {
        var dto = ValidModel();
        dto.Topology = new TopologyDto(); // Empty Nodes collection

        var ex = Assert.Throws<TemplateValidationException>(
            () => RunOrchestrationService.ValidateSimulationModel(dto, TemplateId));
        Assert.Contains("topology", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSimulationModel_DoesNotThrowWhenAllRequiredFieldsPresent()
    {
        var dto = ValidModel();

        // Should not throw.
        RunOrchestrationService.ValidateSimulationModel(dto, TemplateId);
    }

    [Fact]
    public void ValidateSimulationModel_AbsorbsLeakedStateFields_FromSimEmittedYaml()
    {
        // Forward-compatibility guarantee: even though Sim no longer emits
        // leaked-state fields (window:, generator:, top-level metadata:,
        // top-level mode:) post-m-E24-02, the ModelDto intake must silently
        // absorb them if encountered (e.g. older stored YAML, third-party
        // producers) via IgnoreUnmatchedProperties() on the deserializer in
        // ModelService. This pins the absorption invariant so a future strict-
        // deserialization flip cannot land without breaking + replacing this
        // guard.
        var simEmittedYaml = """
            schemaVersion: 1
            generator: flowtime-sim
            mode: simulation
            metadata:
              id: step3-test
              title: Step 3 Test
              version: 1.0.0
            window:
              start: '2026-01-01T00:00:00Z'
              timezone: UTC
            grid:
              bins: 4
              binSize: 5
              binUnit: minutes
              start: '2026-01-01T00:00:00Z'
            topology:
              nodes:
                - id: node1
                  semantics:
                    arrivals: 'arrivals_in:DEFAULT'
                    served: 'served_out:DEFAULT'
              edges: []
              constraints: []
            classes: []
            nodes:
              - id: arrivals_in
                kind: const
                values: [1, 2, 3, 4]
              - id: served_out
                kind: const
                values: [1, 2, 3, 4]
            outputs: []
            provenance:
              generator: flowtime-sim
              generatedAt: '2026-04-25T00:00:00Z'
              templateId: step3-test
              templateVersion: 1.0.0
              mode: simulation
              modelId: model_abc123
            """;

        var dto = FlowTime.Contracts.Services.ModelService.ParseYaml(simEmittedYaml);

        // Should not throw — leaked-state fields absorbed by IgnoreUnmatchedProperties.
        RunOrchestrationService.ValidateSimulationModel(dto, TemplateId);

        // grid.start carries the timestamp regardless of window: presence.
        Assert.Equal("2026-01-01T00:00:00Z", dto.Grid.Start);
        Assert.Equal(4, dto.Grid.Bins);
        Assert.Equal(5, dto.Grid.BinSize);
        Assert.Equal("minutes", dto.Grid.BinUnit);
        Assert.NotNull(dto.Topology);
        Assert.Single(dto.Topology!.Nodes);
    }

    // ---------- BuildSimulationPlanManifest ----------

    [Fact]
    public void BuildSimulationPlanManifest_PopulatesWindowGridAndPendingProvenance_FromModelDto()
    {
        var dto = ValidModel();

        var manifest = RunOrchestrationService.BuildSimulationPlanManifest(dto);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("2026-01-01T00:00:00Z", manifest.Window.StartTimeUtc);
        Assert.Equal(20, manifest.Window.DurationMinutes); // 4 bins × 5 binSize.
        Assert.Equal(4, manifest.Grid.Bins);
        Assert.Equal(5, manifest.Grid.BinSize);
        Assert.Equal("minutes", manifest.Grid.BinUnit);
        Assert.Equal(string.Empty, manifest.Provenance.RunId);
        Assert.Equal("pending", manifest.Provenance.ScenarioHash);
    }

    [Fact]
    public void BuildSimulationPlanManifest_ProducesNullDuration_WhenGridBinsZero()
    {
        var dto = ValidModel();
        // Bypass ValidateSimulationModel by setting bins after the fact;
        // BuildSimulationPlanManifest itself handles Bins<=0 defensively.
        dto.Grid.Bins = 0;

        var manifest = RunOrchestrationService.BuildSimulationPlanManifest(dto);

        Assert.Null(manifest.Window.DurationMinutes);
        Assert.Equal(0, manifest.Grid.Bins);
    }

    [Fact]
    public void BuildSimulationPlanManifest_NormalizesBinUnit_WhenBinUnitEmpty()
    {
        var dto = ValidModel();
        dto.Grid.BinUnit = "";

        var manifest = RunOrchestrationService.BuildSimulationPlanManifest(dto);

        Assert.Equal("minutes", manifest.Grid.BinUnit);
    }

    // ---------- BuildSimulationTelemetryManifest ----------

    [Fact]
    public void BuildSimulationTelemetryManifest_PopulatesProvenanceAndWarnings_FromModelDto()
    {
        var dto = ValidModel();
        var manifestMetadata = StubManifestMetadata("schema-hash-xyz");
        var warnings = new[]
        {
            new RunWarningEntryDto
            {
                Code = "W001",
                Message = "test warning",
                NodeId = "node1",
                Bins = new[] { 1, 2 },
                Severity = "warning"
            }
        };

        var manifest = RunOrchestrationService.BuildSimulationTelemetryManifest(
            dto, manifestMetadata, runId: "run-42", scenarioHash: "scenario-99", runWarnings: warnings);

        Assert.Equal("2026-01-01T00:00:00Z", manifest.Window.StartTimeUtc);
        Assert.Equal(20, manifest.Window.DurationMinutes);
        Assert.Equal(4, manifest.Grid.Bins);
        Assert.Equal("run-42", manifest.Provenance.RunId);
        Assert.Equal("scenario-99", manifest.Provenance.ScenarioHash);
        Assert.Equal("schema-hash-xyz", manifest.Provenance.ModelHash);
        Assert.Single(manifest.Warnings);
        Assert.Equal("W001", manifest.Warnings[0].Code);
        Assert.Equal("warning", manifest.Warnings[0].Severity);
    }

    [Fact]
    public void BuildSimulationTelemetryManifest_DefaultsWarningSeverityToWarning_WhenSeverityBlank()
    {
        var dto = ValidModel();
        var manifestMetadata = StubManifestMetadata(null);
        var warnings = new[]
        {
            new RunWarningEntryDto
            {
                Code = "W002",
                Message = "no severity",
                NodeId = null,
                Bins = Array.Empty<int>(),
                Severity = "" // blank → defaults to "warning"
            }
        };

        var manifest = RunOrchestrationService.BuildSimulationTelemetryManifest(
            dto, manifestMetadata, runId: "run-1", scenarioHash: "h-1", runWarnings: warnings);

        Assert.Single(manifest.Warnings);
        Assert.Equal("warning", manifest.Warnings[0].Severity);
    }

    [Fact]
    public void BuildSimulationTelemetryManifest_HandlesNullWarnings_GracefullyEmitsEmpty()
    {
        var dto = ValidModel();
        var manifestMetadata = StubManifestMetadata(null);

        var manifest = RunOrchestrationService.BuildSimulationTelemetryManifest(
            dto, manifestMetadata, runId: "run-1", scenarioHash: "h-1", runWarnings: null);

        Assert.Empty(manifest.Warnings);
    }

    [Fact]
    public void BuildSimulationTelemetryManifest_PreservesProvidedSeverity_WhenNonBlank()
    {
        var dto = ValidModel();
        var manifestMetadata = StubManifestMetadata(null);
        var warnings = new[]
        {
            new RunWarningEntryDto
            {
                Code = "E001",
                Message = "an error",
                NodeId = "node1",
                Bins = new[] { 0 },
                Severity = "error"
            }
        };

        var manifest = RunOrchestrationService.BuildSimulationTelemetryManifest(
            dto, manifestMetadata, runId: "run-1", scenarioHash: "h-1", runWarnings: warnings);

        Assert.Single(manifest.Warnings);
        Assert.Equal("error", manifest.Warnings[0].Severity);
    }

    // ---------- Helpers ----------

    private static ModelDto ValidModel()
    {
        // Minimum valid post-substitution model: a 4×5min grid with one
        // topology node (no semantics references resolve, but
        // ValidateSimulationModel only checks the structural minimums).
        return new ModelDto
        {
            SchemaVersion = 1,
            Grid = new GridDto
            {
                Bins = 4,
                BinSize = 5,
                BinUnit = "minutes",
                Start = "2026-01-01T00:00:00Z"
            },
            Topology = new TopologyDto
            {
                Nodes =
                {
                    new TopologyNodeDto
                    {
                        Id = "node1",
                        Semantics = new TopologySemanticsDto
                        {
                            Arrivals = "arrivals_in:DEFAULT",
                            Served = "served_out:DEFAULT"
                        }
                    }
                }
            }
        };
    }

    private static FlowTime.Core.TimeTravel.RunManifestMetadata StubManifestMetadata(string? schemaHash)
    {
        return new FlowTime.Core.TimeTravel.RunManifestMetadata
        {
            TemplateId = "stub-template",
            TemplateTitle = "Stub Template",
            TemplateVersion = "1.0.0",
            Mode = "simulation",
            ProvenanceHash = "prov-stub",
            Schema = new FlowTime.Core.TimeTravel.RunSchemaMetadata
            {
                Id = "schema-id",
                Version = "1",
                Hash = schemaHash ?? string.Empty
            },
            Storage = new FlowTime.Core.TimeTravel.RunStorageDescriptor
            {
                ModelPath = "model/model.yaml"
            }
        };
    }
}
