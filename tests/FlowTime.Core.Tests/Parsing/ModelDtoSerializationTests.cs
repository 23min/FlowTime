using FlowTime.Contracts.Dtos;
using FlowTime.Contracts.Services;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Xunit;

namespace FlowTime.Core.Tests.Parsing;

/// <summary>
/// Round-trip and shape tests for additive m-E24-02 step-1 changes to the wire DTOs:
///   - new <see cref="ProvenanceDto"/> with 7 camelCase fields nested on <see cref="ModelDto"/>
///   - <see cref="OutputDto.Exclude"/> mirroring the Sim-side <c>SimOutput.Exclude</c>
///   - <see cref="OutputDto.As"/> nullable (no default), so wire YAML can omit <c>as:</c>
///   - <see cref="NodeDto.Values"/> nullable, so empty/absent values are omitted via
///     <c>DefaultValuesHandling.OmitNull</c> on the serializer (see D-m-E24-02-03)
///   - <see cref="ProvenanceDto.Parameters"/> nullable, omitted via the same mechanism
///
/// Scope discipline: this file covers the DTO surface only — the type contracts and the
/// wire-shape conversions through <see cref="ModelService"/>. Empty-collection / null
/// omission relies on YamlDotNet's <c>DefaultValuesHandling.OmitNull</c> flag set on the
/// serializer (per D-m-E24-02-03, replacing the prior cargo-culted <c>ShouldSerialize*</c>
/// pattern that YamlDotNet does not honor). Schema rewrite (m-E24-03) and validator
/// scalar-style fix (m-E24-04) are out-of-scope here.
/// </summary>
public class ModelDtoSerializationTests
{
    private static ISerializer CreateSerializer() => new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
        .Build();

    // ----- ProvenanceDto -------------------------------------------------------

    [Fact]
    public void ModelDto_HasNullableProvenance_DefaultsToNull()
    {
        var model = new ModelDto();

        Assert.Null(model.Provenance);
    }

    [Fact]
    public void ProvenanceDto_RoundTripsAllSevenFieldsAsCamelCase()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 1
  binSize: 60
  binUnit: minutes
nodes: []
outputs: []
provenance:
  generator: flowtime-sim
  generatedAt: '2026-04-25T10:00:00Z'
  templateId: transportation-basic
  templateVersion: '1.0'
  mode: simulation
  modelId: model-abc-123
  parameters:
    arrivalRate: 5.5
    workerCount: 3
""";

        var model = ModelService.ParseYaml(yaml);

        Assert.NotNull(model.Provenance);
        Assert.Equal("flowtime-sim", model.Provenance!.Generator);
        Assert.Equal("2026-04-25T10:00:00Z", model.Provenance.GeneratedAt);
        Assert.Equal("transportation-basic", model.Provenance.TemplateId);
        Assert.Equal("1.0", model.Provenance.TemplateVersion);
        Assert.Equal("simulation", model.Provenance.Mode);
        Assert.Equal("model-abc-123", model.Provenance.ModelId);
        Assert.NotNull(model.Provenance.Parameters);
        Assert.Equal(2, model.Provenance.Parameters.Count);
        Assert.Contains("arrivalRate", model.Provenance.Parameters.Keys);
        Assert.Contains("workerCount", model.Provenance.Parameters.Keys);
    }

    [Fact]
    public void ProvenanceDto_SerializesWithCamelCaseKeys()
    {
        var model = new ModelDto
        {
            Grid = new GridDto { Bins = 1, BinSize = 60, BinUnit = "minutes" },
            Provenance = new ProvenanceDto
            {
                Generator = "flowtime-sim",
                GeneratedAt = "2026-04-25T10:00:00Z",
                TemplateId = "transportation-basic",
                TemplateVersion = "1.0",
                Mode = "simulation",
                ModelId = "model-abc-123",
                Parameters = new Dictionary<string, object?>
                {
                    ["arrivalRate"] = 5.5,
                    ["workerCount"] = 3,
                }
            }
        };

        var yaml = CreateSerializer().Serialize(model);

        // camelCase wire names — closes the m-E23-01 systematic snake_case violation
        Assert.Contains("generator: flowtime-sim", yaml);
        Assert.Contains("generatedAt:", yaml);
        Assert.Contains("templateId:", yaml);
        Assert.Contains("templateVersion:", yaml);
        Assert.Contains("modelId:", yaml);
        Assert.Contains("mode:", yaml);
        Assert.Contains("parameters:", yaml);

        // explicit casing-violation guard: snake_case forms must not appear in the emission
        Assert.DoesNotContain("generated_at", yaml);
        Assert.DoesNotContain("template_id", yaml);
        Assert.DoesNotContain("template_version", yaml);
        Assert.DoesNotContain("model_id", yaml);
    }

    [Fact]
    public void ProvenanceDto_OmitsBlock_WhenProvenanceIsNull()
    {
        var model = new ModelDto
        {
            Grid = new GridDto { Bins = 1, BinSize = 60, BinUnit = "minutes" },
            Provenance = null,
        };

        var yaml = CreateSerializer().Serialize(model);

        Assert.DoesNotContain("provenance:", yaml);
    }

    [Fact]
    public void ProvenanceDto_DefaultsParametersToNull()
    {
        // Per D-m-E24-02-03, Parameters is nullable; `DefaultValuesHandling.OmitNull` on the
        // serializer suppresses emission of `parameters: {}` when nothing was authored.
        var dto = new ProvenanceDto();

        Assert.Null(dto.Parameters);
    }

    [Fact]
    public void ProvenanceDto_OmitsParameters_WhenNull()
    {
        var model = new ModelDto
        {
            Grid = new GridDto { Bins = 1, BinSize = 60, BinUnit = "minutes" },
            Provenance = new ProvenanceDto
            {
                Generator = "flowtime-sim",
                GeneratedAt = "2026-04-25T10:00:00Z",
                TemplateId = "transportation-basic",
                TemplateVersion = "1.0",
                Mode = "simulation",
                ModelId = "model-abc-123",
                Parameters = null,
            }
        };

        var yaml = CreateSerializer().Serialize(model);

        Assert.DoesNotContain("parameters:", yaml);
    }

    [Fact]
    public void ProvenanceDto_EmitsParameters_WhenNotEmpty()
    {
        var model = new ModelDto
        {
            Grid = new GridDto { Bins = 1, BinSize = 60, BinUnit = "minutes" },
            Provenance = new ProvenanceDto
            {
                Generator = "flowtime-sim",
                GeneratedAt = "2026-04-25T10:00:00Z",
                TemplateId = "transportation-basic",
                TemplateVersion = "1.0",
                Mode = "simulation",
                ModelId = "model-abc-123",
                Parameters = new Dictionary<string, object?>
                {
                    ["arrivalRate"] = 5.5,
                }
            }
        };

        var yaml = CreateSerializer().Serialize(model);

        Assert.Contains("parameters:", yaml);
        Assert.Contains("arrivalRate", yaml);
    }

    // ----- OutputDto.Exclude ---------------------------------------------------

    [Fact]
    public void OutputDto_Exclude_DefaultsToNull()
    {
        var output = new OutputDto();

        Assert.Null(output.Exclude);
    }

    [Fact]
    public void OutputDto_Exclude_RoundTripsAsList()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 1
  binSize: 60
  binUnit: minutes
nodes: []
outputs:
  - series: '*'
    exclude:
      - debug_series
      - internal_series
""";

        var model = ModelService.ParseYaml(yaml);

        Assert.Single(model.Outputs);
        Assert.NotNull(model.Outputs[0].Exclude);
        Assert.Equal(new[] { "debug_series", "internal_series" }, model.Outputs[0].Exclude!);
    }

    [Fact]
    public void OutputDto_Exclude_OmittedFromYaml_WhenNull()
    {
        var model = new ModelDto
        {
            Grid = new GridDto { Bins = 1, BinSize = 60, BinUnit = "minutes" },
            Outputs = new List<OutputDto>
            {
                new() { Series = "*", As = "out.csv", Exclude = null }
            }
        };

        var yaml = CreateSerializer().Serialize(model);

        Assert.DoesNotContain("exclude:", yaml);
    }

    [Fact]
    public void OutputDto_Exclude_EmittedToYaml_WhenSet()
    {
        var model = new ModelDto
        {
            Grid = new GridDto { Bins = 1, BinSize = 60, BinUnit = "minutes" },
            Outputs = new List<OutputDto>
            {
                new() { Series = "*", Exclude = new List<string> { "debug" } }
            }
        };

        var yaml = CreateSerializer().Serialize(model);

        Assert.Contains("exclude:", yaml);
        Assert.Contains("- debug", yaml);
    }

    // ----- OutputDto.As becomes nullable --------------------------------------

    [Fact]
    public void OutputDto_As_DefaultsToNull()
    {
        var output = new OutputDto();

        Assert.Null(output.As);
    }

    [Fact]
    public void OutputDto_As_OmittedFromYaml_WhenNull()
    {
        var model = new ModelDto
        {
            Grid = new GridDto { Bins = 1, BinSize = 60, BinUnit = "minutes" },
            Outputs = new List<OutputDto>
            {
                new() { Series = "served_count", As = null }
            }
        };

        var yaml = CreateSerializer().Serialize(model);

        Assert.DoesNotContain("as:", yaml);
    }

    [Fact]
    public void OutputDto_As_RoundTripsWhenPresent()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 1
  binSize: 60
  binUnit: minutes
nodes: []
outputs:
  - series: served_count
    as: served.csv
""";

        var model = ModelService.ParseYaml(yaml);

        Assert.Single(model.Outputs);
        Assert.Equal("served.csv", model.Outputs[0].As);
    }

    [Fact]
    public void OutputDto_As_DeserializesAsNull_WhenAbsentFromYaml()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 1
  binSize: 60
  binUnit: minutes
nodes: []
outputs:
  - series: served_count
""";

        var model = ModelService.ParseYaml(yaml);

        Assert.Single(model.Outputs);
        Assert.Null(model.Outputs[0].As);
    }

    [Fact]
    public void ModelService_ConvertToModelDefinition_HandlesNullAs_WithoutThrowing()
    {
        // OutputDefinition.As is non-nullable string — converter must coerce null OutputDto.As to "".
        var dto = new ModelDto
        {
            Grid = new GridDto { Bins = 1, BinSize = 60, BinUnit = "minutes" },
            Nodes =
            {
                new NodeDto { Id = "n1", Kind = "const", Values = new[] { 1.0 } }
            },
            Outputs =
            {
                new OutputDto { Series = "n1", As = null }
            }
        };

        var def = ModelService.ConvertToModelDefinition(dto);

        Assert.Single(def.Outputs);
        Assert.NotNull(def.Outputs[0].As);
        Assert.Equal(string.Empty, def.Outputs[0].As);
    }

    [Fact]
    public void ModelService_ConvertToModelDefinition_PreservesNonNullAs()
    {
        // Pins the non-null branch of the `o.As ?? string.Empty` coalesce.
        var dto = new ModelDto
        {
            Grid = new GridDto { Bins = 1, BinSize = 60, BinUnit = "minutes" },
            Nodes =
            {
                new NodeDto { Id = "n1", Kind = "const", Values = new[] { 1.0 } }
            },
            Outputs =
            {
                new OutputDto { Series = "n1", As = "explicit.csv" }
            }
        };

        var def = ModelService.ConvertToModelDefinition(dto);

        Assert.Single(def.Outputs);
        Assert.Equal("explicit.csv", def.Outputs[0].As);
    }

    // ----- NodeDto.ShouldSerializeValues ---------------------------------------

    [Fact]
    public void NodeDto_Values_OmittedFromYaml_WhenNull()
    {
        var model = new ModelDto
        {
            Grid = new GridDto { Bins = 1, BinSize = 60, BinUnit = "minutes" },
            Nodes =
            {
                new NodeDto { Id = "n1", Kind = "expr", Expr = "1", Values = null }
            }
        };

        var yaml = CreateSerializer().Serialize(model);

        Assert.DoesNotContain("values:", yaml);
    }

    [Fact]
    public void NodeDto_Values_EmittedToYaml_WhenPopulated()
    {
        var model = new ModelDto
        {
            Grid = new GridDto { Bins = 2, BinSize = 60, BinUnit = "minutes" },
            Nodes =
            {
                new NodeDto { Id = "n1", Kind = "const", Values = new[] { 1.0, 2.0 } }
            }
        };

        var yaml = CreateSerializer().Serialize(model);

        Assert.Contains("values:", yaml);
    }

}
