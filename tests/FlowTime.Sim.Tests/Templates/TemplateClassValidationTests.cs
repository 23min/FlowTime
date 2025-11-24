using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Sim.Tests.Templates;

public class TemplateClassValidationTests
{
    [Fact]
    public async Task ValidateParameters_Fails_On_Undeclared_Class()
    {
        const string templateId = "class-invalid";
        var templates = new Dictionary<string, string>
        {
            {
                templateId,
                """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: class-invalid
  title: Invalid Class Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 4
  binSize: 1
  binUnit: hours
classes:
  - id: Order
nodes:
  - id: ingest
    kind: const
    values: [1, 1, 1, 1]
  - id: served
    kind: const
    values: [1, 1, 1, 1]
traffic:
  arrivals:
    - nodeId: ingest
      classId: Refund
      pattern:
        kind: constant
        ratePerBin: 5
topology:
  nodes:
    - id: Ingest
      kind: service
      semantics:
        arrivals: ingest
        served: served
        errors: served
outputs:
  - series: served
    as: served.csv
"""
            }
        };

        var service = new TemplateService(templates, NullLogger<TemplateService>.Instance);

        var result = await service.ValidateParametersAsync(templateId, new Dictionary<string, object>());

        Assert.False(result.IsValid);
        Assert.Contains("Refund", string.Join("; ", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateParameters_Succeeds_With_MultipleClasses()
    {
        const string templateId = "class-valid";
        var templates = new Dictionary<string, string>
        {
            {
                templateId,
                """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: class-valid
  title: Valid Class Template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 4
  binSize: 1
  binUnit: hours
classes:
  - id: Order
  - id: Refund
nodes:
  - id: ingest
    kind: const
    values: [1, 1, 1, 1]
  - id: served
    kind: const
    values: [1, 1, 1, 1]
traffic:
  arrivals:
    - nodeId: ingest
      classId: Order
      pattern:
        kind: constant
        ratePerBin: 5
    - nodeId: ingest
      classId: Refund
      pattern:
        kind: constant
        ratePerBin: 2
topology:
  nodes:
    - id: Ingest
      kind: service
      semantics:
        arrivals: ingest
        served: served
        errors: served
outputs:
  - series: served
    as: served.csv
"""
            }
        };

        var service = new TemplateService(templates, NullLogger<TemplateService>.Instance);

        var result = await service.ValidateParametersAsync(templateId, new Dictionary<string, object>());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
