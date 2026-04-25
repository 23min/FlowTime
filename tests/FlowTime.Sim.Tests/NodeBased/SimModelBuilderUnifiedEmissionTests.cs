using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowTime.Contracts.Dtos;
using FlowTime.Contracts.Services;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Sim.Tests.NodeBased;

/// <summary>
/// m-E24-02 step 4 — wire-format evidence that <see cref="SimModelBuilder"/>
/// emits the unified <see cref="ModelDto"/> shape and that the four leaked-state
/// root fields (<c>window</c>, top-level <c>generator</c>, top-level <c>metadata</c>,
/// top-level <c>mode</c>) per AC6 are absent. The four fields are not silently
/// renamed; they are removed, with <c>generator</c> and <c>mode</c> migrating
/// into the unified <c>provenance</c> block per Q5/A4. The test also covers Q4
/// (no <c>nodes[].source</c>), Q5 (no <c>provenance.source</c> /
/// <c>provenance.schemaVersion</c>), and the camelCase guarantee (no snake_case
/// keys on the unified provenance block).
/// </summary>
[Trait("Category", "NodeBased")]
public sealed class SimModelBuilderUnifiedEmissionTests
{
    private const string MinimalTemplate = """
metadata:
  id: emission-evidence-minimal
  title: Minimal Emission Evidence Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 4
  binSize: 30
  binUnit: minutes
topology:
  nodes:
    - id: SimpleService
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [10, 20, 30, 40]
  - id: served
    kind: const
    values: [9, 18, 27, 36]
outputs:
  - series: "*"
""";

    [Fact]
    public async Task Emission_DropsTopLevelLeakedStateFields_PerAC6()
    {
        var (yaml, _) = await GenerateAsync(MinimalTemplate, "emission-evidence-minimal");

        // AC6: each of the four leaked-state root fields is absent at the
        // top level. We grep at the line-anchor `^<key>:` so we don't false-
        // positive on `provenance.generator:`, `provenance.mode:`, or any
        // sub-block keys that share the name.
        var lines = yaml.Split('\n');

        Assert.DoesNotContain(lines, l => l.StartsWith("window:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("metadata:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("generator:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("mode:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Emission_PreservesGeneratorAndModeInsideProvenance_PerQ5A4()
    {
        var (yaml, model) = await GenerateAsync(MinimalTemplate, "emission-evidence-minimal");

        // Q5/A4: generator + mode survive inside the unified provenance block.
        // This is the explicit migration path for the two leaked-state fields
        // that carry forensic value (vs. window/metadata which drop entirely).
        Assert.NotNull(model.Provenance);
        Assert.False(string.IsNullOrWhiteSpace(model.Provenance!.Generator),
            "Q5/A4: provenance.generator must carry the producing-system identifier.");
        Assert.False(string.IsNullOrWhiteSpace(model.Provenance.Mode),
            "Q5/A4: provenance.mode must carry the template mode.");

        // YAML wire evidence — both fields appear inside the `provenance:` block,
        // not at the top level. We confirm the indented form is emitted.
        Assert.Contains("provenance:", yaml);
        Assert.Contains("  generator: ", yaml);
        Assert.Contains("  mode: ", yaml);
    }

    [Fact]
    public async Task Emission_ProvenanceBlockUsesAllSevenCamelCaseFields_PerQ5A4()
    {
        var (yaml, model) = await GenerateAsync(MinimalTemplate, "emission-evidence-minimal");

        // Q5/A4: the unified provenance block has exactly the seven ratified
        // fields, all camelCase.
        Assert.NotNull(model.Provenance);

        // All seven fields populated on the round-trip DTO.
        Assert.False(string.IsNullOrWhiteSpace(model.Provenance!.Generator));
        Assert.False(string.IsNullOrWhiteSpace(model.Provenance.GeneratedAt));
        Assert.False(string.IsNullOrWhiteSpace(model.Provenance.TemplateId));
        Assert.False(string.IsNullOrWhiteSpace(model.Provenance.TemplateVersion));
        Assert.False(string.IsNullOrWhiteSpace(model.Provenance.Mode));
        Assert.False(string.IsNullOrWhiteSpace(model.Provenance.ModelId));
        // Parameters may be null when no template parameter values were supplied
        // (D-m-E24-02-03 — nullable + OmitNull).

        // Camel-case wire evidence — every key under provenance is camelCase.
        // None of the snake_case forms (the m-E23-01 violations) appear.
        Assert.Contains("  generator: ", yaml);
        Assert.Contains("  generatedAt: ", yaml);
        Assert.Contains("  templateId: ", yaml);
        Assert.Contains("  templateVersion: ", yaml);
        Assert.Contains("  mode: ", yaml);
        Assert.Contains("  modelId: ", yaml);

        Assert.DoesNotContain("generated_at", yaml);
        Assert.DoesNotContain("template_id", yaml);
        Assert.DoesNotContain("template_version", yaml);
        Assert.DoesNotContain("model_id", yaml);
    }

    [Fact]
    public async Task Emission_DoesNotIncludeProvenanceSourceOrProvenanceSchemaVersion_PerQ5()
    {
        var (yaml, _) = await GenerateAsync(MinimalTemplate, "emission-evidence-minimal");

        // Q5: provenance.source dropped (collapsed into provenance.generator).
        // Q5: provenance.schemaVersion dropped (root carries schemaVersion).
        Assert.DoesNotContain("  source:", yaml);
        Assert.DoesNotContain("  schemaVersion:", yaml);
    }

    [Fact]
    public async Task Emission_DropsNodesSourceField_PerQ4()
    {
        // Build a template that authors `nodes[].source:` (the authoring-time
        // field on TemplateNode that survives per Q4) and verify the Sim emitter
        // does NOT propagate it onto the wire model.
        const string templateWithSource = """
metadata:
  id: emission-evidence-source
  title: Source Drop Evidence
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 2
  binSize: 5
  binUnit: minutes
topology:
  nodes:
    - id: SimpleService
      kind: service
      semantics:
        arrivals: arrivals
        served: served
  edges: []
nodes:
  - id: arrivals
    kind: const
    values: [120, 130]
    source: file://telemetry/arrivals.csv
  - id: served
    kind: const
    values: [110, 115]
outputs:
  - series: "*"
""";

        var (yaml, model) = await GenerateAsync(templateWithSource, "emission-evidence-source", TemplateMode.Telemetry);

        // Q4: nodes[].source is dropped from Sim emission and not declared on
        // NodeDto. The wire YAML must not carry the authoring-time field.
        Assert.DoesNotContain("source: file://telemetry/arrivals.csv", yaml);

        // Regardless: NodeDto has no Source property — round-tripping the
        // YAML produces a model where the field literally does not exist.
        Assert.Contains(model.Nodes, n => n.Id == "arrivals");
    }

    [Fact]
    public async Task Emission_TopLevelSchemaVersionStillPresent()
    {
        var (yaml, model) = await GenerateAsync(MinimalTemplate, "emission-evidence-minimal");

        // schemaVersion stays at the root (per Q5 the duplicate inside provenance
        // dropped — the root field is the canonical version identifier).
        Assert.Equal(1, model.SchemaVersion);
        Assert.StartsWith("schemaVersion: 1", yaml);
    }

    [Fact]
    public async Task Emission_GridStartCarriesWindowStart_PerA1A2A3()
    {
        var (_, model) = await GenerateAsync(MinimalTemplate, "emission-evidence-minimal");

        // A1+A2+A3: grid.start is the canonical wire field for the window-start
        // timestamp (the prior LegacyStart shim is gone). The Sim emitter
        // populates grid.start from template.window.start when grid.start is
        // not authored explicitly (existing behavior preserved through the
        // SimModelBuilder rewrite).
        Assert.Equal("2025-01-01T00:00:00Z", model.Grid.Start);
    }

    private static async System.Threading.Tasks.Task<(string yaml, ModelDto model)> GenerateAsync(
        string templateYaml,
        string templateId,
        TemplateMode? modeOverride = null)
    {
        var service = new TemplateService(
            new Dictionary<string, string> { [templateId] = templateYaml },
            NullLogger<TemplateService>.Instance);
        var yaml = await service.GenerateEngineModelAsync(templateId, new Dictionary<string, object>(), modeOverride);
        var model = ModelService.ParseYaml(yaml);
        return (yaml, model);
    }
}
