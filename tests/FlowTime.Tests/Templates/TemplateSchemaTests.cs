using FlowTime.Core;

namespace FlowTime.Tests.Templates;

public class TemplateSchemaTests
{
    [Fact]
    public void TemplateSchema_Allows_ClassDeclarations()
    {
        var yaml = """
schemaVersion: 1
classes:
  - id: Order
    displayName: Order Flow
  - id: Refund
    displayName: Refund Flow
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: ingest
    kind: const
    values: [10, 10, 10, 10]
traffic:
  arrivals:
    - nodeId: ingest
      classId: Order
      pattern:
        kind: constant
        ratePerBin: 20
    - nodeId: ingest
      classId: Refund
      pattern:
        kind: constant
        ratePerBin: 5
""";

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void TemplateSchema_Rejects_UnknownClassReference()
    {
        var yaml = """
schemaVersion: 1
classes:
  - id: Order
    displayName: Order Flow
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: ingest
    kind: const
    values: [10, 10, 10, 10]
traffic:
  arrivals:
    - nodeId: ingest
      classId: Refund
      pattern:
        kind: constant
        ratePerBin: 20
""";

        var result = ModelSchemaValidator.Validate(yaml);
        var errors = string.Join("; ", result.Errors);

        Assert.False(result.IsValid);
        Assert.Contains("Refund", errors, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TemplateSchema_Defaults_ToWildcard_WhenOmitted()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: ingest
    kind: const
    values: [10, 10, 10, 10]
traffic:
  arrivals:
    - nodeId: ingest
      pattern:
        kind: constant
        ratePerBin: 20
""";

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }
}
