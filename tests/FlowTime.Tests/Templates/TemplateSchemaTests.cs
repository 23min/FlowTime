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

    [Fact]
    public void TemplateSchema_Allows_RouterDefinitions()
    {
        var yaml = """
schemaVersion: 1
classes:
  - id: Airport
  - id: Downtown
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: hub_queue
    kind: const
    values: [10, 12, 14]
  - id: airport_arrivals
    kind: expr
    expr: "hub_queue * 0.4"
  - id: downtown_arrivals
    kind: expr
    expr: "hub_queue * 0.6"
  - id: hub_router
    kind: router
    inputs:
      queue: hub_queue
    routes:
      - target: airport_arrivals
        classes: [Airport]
      - target: downtown_arrivals
        weight: 1
traffic:
  arrivals:
    - nodeId: hub_queue
      classId: Airport
      pattern:
        kind: constant
        ratePerBin: 5
""";

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void TemplateSchema_Router_Requires_Target()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: hub_queue
    kind: const
    values: [10, 10]
  - id: invalid_router
    kind: router
    inputs:
      queue: hub_queue
    routes:
      - weight: 1
""";

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains("routes", string.Join("; ", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TemplateSchema_ServiceWithBuffer_Node_Is_Valid()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: hub_demand
    kind: const
    values: [10, 12]
  - id: hub_dispatch
    kind: expr
    expr: "hub_demand * 0.9"
  - id: hub_queue
    kind: serviceWithBuffer
    inflow: hub_demand
    outflow: hub_dispatch
traffic:
  arrivals:
    - nodeId: hub_demand
      pattern:
        kind: constant
        ratePerBin: 10
""";

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void TemplateSchema_ServiceWithBuffer_Allows_Self_QueueDepth()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 1
  binUnit: hours
topology:
  nodes:
    - id: wave
      kind: serviceWithBuffer
      semantics:
        arrivals: inbound_orders
        served: outbound_orders
        queueDepth: self
nodes:
  - id: inbound_orders
    kind: const
    values: [10, 12]
  - id: outbound_orders
    kind: const
    values: [9, 11]
""";

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void TemplateSchema_Queue_Allows_Self_QueueDepth()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 1
  binUnit: hours
topology:
  nodes:
    - id: picker_queue
      kind: queue
      semantics:
        arrivals: staged_orders
        served: released_orders
        queueDepth: self
nodes:
  - id: staged_orders
    kind: const
    values: [15, 20]
  - id: released_orders
    kind: const
    values: [10, 10]
""";

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void TemplateSchema_Dlq_Allows_Self_QueueDepth()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 1
  binUnit: hours
topology:
  nodes:
    - id: airport_dlq
      kind: dlq
      semantics:
        arrivals: failed_arrivals
        served: purge_series
        errors: attrition_series
        queueDepth: self
nodes:
  - id: failed_arrivals
    kind: const
    values: [5, 7]
  - id: purge_series
    kind: const
    values: [0, 0]
  - id: attrition_series
    kind: const
    values: [0, 0]
""";

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void TemplateSchema_Backlog_Node_Is_Rejected()
    {
        var yaml = """
schemaVersion: 1
grid:
  bins: 2
  binSize: 1
  binUnit: hours
nodes:
  - id: hub_demand
    kind: const
    values: [10, 12]
  - id: hub_dispatch
    kind: expr
    expr: "hub_demand * 0.9"
  - id: hub_queue
    kind: backlog
    inflow: hub_demand
    outflow: hub_dispatch
traffic:
  arrivals:
    - nodeId: hub_demand
      pattern:
        kind: constant
        ratePerBin: 10
""";

        var result = ModelSchemaValidator.Validate(yaml);
        Assert.False(result.IsValid);
    }
}
