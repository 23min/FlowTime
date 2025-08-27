using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class ServiceSpecParserTests
{
    private static SimulationSpec BaseSpec(string serviceYaml) => SimulationSpecLoader.LoadFromString($@"schemaVersion: 1
rng: pcg
grid:
  bins: 2
  binMinutes: 60
  start: 2025-01-01T00:00:00Z
seed: 123
arrivals:
  kind: const
  values: [1,1]
route:
  id: nodeA
service:
{serviceYaml}");

    [Fact]
    public void Const_Service_Valid()
    {
        var spec = BaseSpec("  kind: const\n  value: 5");
        var result = SimulationSpecValidator.Validate(spec);
        Assert.True(result.IsValid, string.Join(";", result.Errors));
    }

    [Fact]
    public void Exp_Service_Valid()
    {
        var spec = BaseSpec("  kind: exp\n  rate: 2.5");
        var result = SimulationSpecValidator.Validate(spec);
        Assert.True(result.IsValid, string.Join(";", result.Errors));
    }

    [Fact]
    public void Const_Service_MissingValue_IsError()
    {
        var spec = BaseSpec("  kind: const");
        var result = SimulationSpecValidator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains("service.value: required for kind=const", result.Errors);
    }

    [Fact]
    public void Exp_Service_MissingRate_IsError()
    {
        var spec = BaseSpec("  kind: exp");
        var result = SimulationSpecValidator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains("service.rate: required for kind=exp", result.Errors);
    }

    [Fact]
    public void Const_Service_RejectsRate()
    {
        var spec = BaseSpec("  kind: const\n  value: 1\n  rate: 2");
        var result = SimulationSpecValidator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains("service: rate must not be set for kind=const", result.Errors);
    }

    [Fact]
    public void Exp_Service_RejectsValue()
    {
        var spec = BaseSpec("  kind: exp\n  rate: 2.5\n  value: 10");
        var result = SimulationSpecValidator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains("service: value must not be set for kind=exp", result.Errors);
    }

    [Fact]
    public void Exp_Service_RateMustBePositive()
    {
        var spec = BaseSpec("  kind: exp\n  rate: 0");
        var result = SimulationSpecValidator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains("service.rate: must be > 0", result.Errors);
    }
}
