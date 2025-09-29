using FlowTime.Sim.Core.Templates;
using Xunit;

namespace FlowTime.Sim.Tests.NodeBased;

[Trait("Category", "NodeBased")]
public class TemplateParserTests
{
    [Fact]
    public void Valid_Const_Node_Template_Parses_Successfully()
    {
        var yaml = """
metadata:
  id: simple-const-template
  title: Simple Constant Template
  description: A basic template with constant values
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: [100, 150, 200]
outputs:
  - id: arrival_series
    source: arrivals
    description: Arrival counts per time bin
""";

        var template = TemplateParser.ParseFromYaml(yaml);
        
        Assert.NotNull(template);
        Assert.Equal("simple-const-template", template.Metadata.Id);
        Assert.Equal("Simple Constant Template", template.Metadata.Title);
        Assert.Equal(3, template.Grid.Bins);
        Assert.Equal(60, template.Grid.BinSize);
        Assert.Equal("minutes", template.Grid.BinUnit);
        
        Assert.Single(template.Nodes);
        var node = template.Nodes[0];
        Assert.Equal("arrivals", node.Id);
        Assert.Equal("const", node.Kind);
        Assert.Equal(new[] { 100.0, 150.0, 200.0 }, node.Values);
        
        Assert.Single(template.Outputs);
        var output = template.Outputs[0];
        Assert.Equal("arrival_series", output.Id);
        Assert.Equal("arrivals", output.Source);
    }

    [Fact]
    public void Valid_PMF_Node_Template_Parses_Successfully()
    {
        var yaml = """
metadata:
  id: pmf-template
  title: PMF Template
  description: Template with PMF node
grid:
  bins: 4
  binSize: 15
  binUnit: minutes
nodes:
  - id: stochastic_arrivals
    kind: pmf
    pmf:
      values: [10, 20, 30]
      probabilities: [0.3, 0.5, 0.2]
outputs:
  - id: arrival_series
    source: stochastic_arrivals
    description: Stochastic arrival counts
""";

        var template = TemplateParser.ParseFromYaml(yaml);
        
        Assert.NotNull(template);
        Assert.Equal("pmf-template", template.Metadata.Id);
        Assert.Equal(4, template.Grid.Bins);
        
        Assert.Single(template.Nodes);
        var node = template.Nodes[0];
        Assert.Equal("stochastic_arrivals", node.Id);
        Assert.Equal("pmf", node.Kind);
        Assert.NotNull(node.Pmf);
        Assert.Equal(new[] { 10.0, 20.0, 30.0 }, node.Pmf.Values);
        Assert.Equal(new[] { 0.3, 0.5, 0.2 }, node.Pmf.Probabilities);
    }

    [Fact]
    public void Template_With_Parameters_Parses_Successfully()
    {
        var yaml = """
metadata:
  id: parameterized-template
  title: Parameterized Template
parameters:
  - name: arrival_rate
    type: number
    title: Arrival Rate
    description: Average arrivals per time bin
    default: 100
    min: 0
    max: 1000
  - name: grid_size
    type: integer
    title: Grid Size
    description: Number of time bins
    default: 5
    min: 1
    max: 100
grid:
  bins: "{{grid_size}}"
  binSize: 60
  binUnit: minutes
nodes:
  - id: arrivals
    kind: const
    values: "{{arrival_rate}}"
outputs:
  - id: arrival_series
    source: arrivals
""";

        var template = TemplateParser.ParseFromYaml(yaml);
        
        Assert.NotNull(template);
        Assert.Equal("parameterized-template", template.Metadata.Id);
        Assert.Equal(2, template.Parameters.Count);
        
        var arrivalRateParam = template.Parameters.First(p => p.Name == "arrival_rate");
        Assert.Equal("number", arrivalRateParam.Type);
        Assert.Equal(100.0, arrivalRateParam.Default);
        Assert.Equal(0.0, arrivalRateParam.Min);
        Assert.Equal(1000.0, arrivalRateParam.Max);
        
        var gridSizeParam = template.Parameters.First(p => p.Name == "grid_size");
        Assert.Equal("integer", gridSizeParam.Type);
        Assert.Equal(5, gridSizeParam.Default);
    }

    [Fact]
    public void Invalid_Template_Missing_Required_Fields_Throws_Exception()
    {
        var yaml = """
metadata:
  id: incomplete-template
grid:
  bins: 3
# Missing nodes and outputs
""";

        Assert.Throws<TemplateParsingException>(() => TemplateParser.ParseFromYaml(yaml));
    }

    [Fact]
    public void Invalid_Node_Kind_Throws_Exception()
    {
        var yaml = """
metadata:
  id: invalid-node-template
  title: Invalid Node Template
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: invalid_node
    kind: invalid_kind
    values: [100]
outputs:
  - id: output
    source: invalid_node
""";

        Assert.Throws<TemplateParsingException>(() => TemplateParser.ParseFromYaml(yaml));
    }

    [Fact]
    public void Template_Validation_Detects_Invalid_PMF_Probabilities()
    {
        var yaml = """
metadata:
  id: invalid-pmf-template
  title: Invalid PMF Template
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: bad_pmf
    kind: pmf
    pmf:
      values: [10, 20, 30]
      probabilities: [0.3, 0.5, 0.3]  # Sum > 1.0
outputs:
  - id: output
    source: bad_pmf
""";

        Assert.Throws<TemplateValidationException>(() => TemplateParser.ParseFromYaml(yaml));
    }

    [Fact]
    public void Template_With_Expression_Node_Parses_Successfully()
    {
        var yaml = """
metadata:
  id: expression-template
  title: Expression Template
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: base_rate
    kind: const
    values: [100, 150, 200]
  - id: multiplied_rate
    kind: expr
    expression: "base_rate * 1.5"
    dependencies: [base_rate]
outputs:
  - id: final_series
    source: multiplied_rate
""";

        var template = TemplateParser.ParseFromYaml(yaml);
        
        Assert.NotNull(template);
        Assert.Equal(2, template.Nodes.Count);
        
        var exprNode = template.Nodes.First(n => n.Kind == "expr");
        Assert.Equal("multiplied_rate", exprNode.Id);
        Assert.Equal("base_rate * 1.5", exprNode.Expression);
        Assert.Equal(new[] { "base_rate" }, exprNode.Dependencies);
    }
}