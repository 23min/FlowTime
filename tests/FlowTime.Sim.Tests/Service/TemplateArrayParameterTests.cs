using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using FlowTime.Sim.Core.Templates.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Tests.Service;

public class TemplateArrayParameterTests
{
    [Fact]
    public void Normalize_ShouldConvertJsonArrayToDoubleArray()
    {
        var parameter = new TemplateParameter
        {
            Name = "baseLoad",
            Type = "array",
            ArrayOf = "double"
        };

        using var doc = JsonDocument.Parse("[1.25, 2.5, 3.75]");

        var normalized = TemplateParameterValueConverter.Normalize(parameter, doc.RootElement);

        var values = Assert.IsType<double[]>(normalized);
        Assert.Equal(new[] { 1.25, 2.5, 3.75 }, values);
    }

    [Fact]
    public void Normalize_ShouldConvertJsonArrayToIntArray()
    {
        var parameter = new TemplateParameter
        {
            Name = "binsByHour",
            Type = "array",
            ArrayOf = "int"
        };

        using var doc = JsonDocument.Parse("[12, 24, 36]");

        var normalized = TemplateParameterValueConverter.Normalize(parameter, doc.RootElement);

        var values = Assert.IsType<int[]>(normalized);
        Assert.Equal(new[] { 12, 24, 36 }, values);
    }

    [Fact]
    public void Normalize_DefaultsToDoubleArrayWhenArrayOfMissing()
    {
        var parameter = new TemplateParameter
        {
            Name = "baseLoad",
            Type = "array",
            ArrayOf = null
        };

        using var doc = JsonDocument.Parse("[5, 10, 15]");

        var normalized = TemplateParameterValueConverter.Normalize(parameter, doc.RootElement);

        var values = Assert.IsType<double[]>(normalized);
        Assert.Equal(new[] { 5d, 10d, 15d }, values);
    }

    [Fact]
    public async Task GenerateEngineModelAsync_ShouldBindArraysWhenPlaceholderQuoted()
    {
        const string yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: quoted-array
  title: Quoted Array
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
parameters:
  - name: baseLoad
    type: array
    arrayOf: double
topology:
  nodes:
    - id: service
      kind: service
      semantics:
        arrivals: base_requests
        served: base_requests
  edges: []
nodes:
  - id: base_requests
    kind: const
    values: "${baseLoad}"
outputs:
  - id: base_series
    series: base_requests
""";

        using var doc = JsonDocument.Parse("[1.0, 2.0, 3.0]");
        var parameters = new Dictionary<string, object>
        {
            ["baseLoad"] = doc.RootElement
        };

        var service = new TemplateService(
            new Dictionary<string, string> { ["quoted-array"] = yaml },
            NullLogger<TemplateService>.Instance);

        var artifactYaml = await service.GenerateEngineModelAsync("quoted-array", parameters);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var artifact = deserializer.Deserialize<SimModelArtifact>(artifactYaml);

        var node = artifact.Nodes.First(n => n.Id == "base_requests");
        Assert.Equal(new[] { 1d, 2d, 3d }, node.Values);
    }

    [Fact]
    public async Task GenerateEngineModelAsync_NetworkReliabilityTemplate_Succeeds()
    {
        var templatePath = ResolveTemplatePath("network-reliability.yaml");
        var yaml = await File.ReadAllTextAsync(templatePath);
        var service = new TemplateService(
            new Dictionary<string, string> { ["network-reliability"] = yaml },
            NullLogger<TemplateService>.Instance);

        var parameters = new Dictionary<string, object>
        {
            ["rngSeed"] = 42
        };

        var model = await service.GenerateEngineModelAsync("network-reliability", parameters);

        Assert.False(string.IsNullOrWhiteSpace(model));

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var artifact = deserializer.Deserialize<SimModelArtifact>(model);
        Assert.NotNull(artifact);
    }

    [Fact]
    public async Task GenerateEngineModelAsync_NetworkReliabilityFromFilesystem_Succeeds()
    {
        var templatePath = ResolveTemplatePath("network-reliability.yaml");
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"network-template-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var destination = Path.Combine(tempDirectory, "network-reliability.yaml");
            File.Copy(templatePath, destination, overwrite: true);

            var service = new TemplateService(tempDirectory, NullLogger<TemplateService>.Instance);
            var parameters = new Dictionary<string, object>
            {
                ["rngSeed"] = 42
            };

            var model = await service.GenerateEngineModelAsync("network-reliability", parameters);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var artifact = deserializer.Deserialize<SimModelArtifact>(model);
            Assert.NotNull(artifact);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateEngineModelAsync_NetworkReliabilityDefaults_NoOverrides_Succeeds()
    {
        var templatePath = ResolveTemplatePath("network-reliability.yaml");
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"network-template-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            File.Copy(templatePath, Path.Combine(tempDirectory, "network-reliability.yaml"), overwrite: true);

            var service = new TemplateService(tempDirectory, NullLogger<TemplateService>.Instance);
            var model = await service.GenerateEngineModelAsync("network-reliability", new Dictionary<string, object>());

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var artifact = deserializer.Deserialize<SimModelArtifact>(model);
            Assert.NotNull(artifact);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateEngineModelAsync_ShouldRejectArraysExceedingMaximum()
    {
        const string yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: array-max
  title: Array Max Validation
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
parameters:
  - name: baseLoad
    type: array
    arrayOf: double
    minimum: 0
    maximum: 100
topology:
  nodes:
    - id: service
      kind: service
      semantics:
        arrivals: base_requests
        served: base_requests
  edges: []
nodes:
  - id: base_requests
    kind: const
    values: ${baseLoad}
outputs:
  - id: base_series
    series: base_requests
""";

        using var doc = JsonDocument.Parse("[10, 20, 150]");
        var parameters = new Dictionary<string, object>
        {
            ["baseLoad"] = doc.RootElement
        };

        var service = new TemplateService(
            new Dictionary<string, string> { ["array-max"] = yaml },
            NullLogger<TemplateService>.Instance);

        var ex = await Assert.ThrowsAsync<TemplateValidationException>(() =>
            service.GenerateEngineModelAsync("array-max", parameters));

        Assert.Contains("maximum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateEngineModelAsync_ShouldRejectArraysBelowMinimum()
    {
        const string yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: array-min
  title: Array Min Validation
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
parameters:
  - name: baseLoad
    type: array
    arrayOf: double
    minimum: 0
    maximum: 100
topology:
  nodes:
    - id: service
      kind: service
      semantics:
        arrivals: base_requests
        served: base_requests
  edges: []
nodes:
  - id: base_requests
    kind: const
    values: ${baseLoad}
outputs:
  - id: base_series
    series: base_requests
""";

        using var doc = JsonDocument.Parse("[-5, 10, 15]");
        var parameters = new Dictionary<string, object>
        {
            ["baseLoad"] = doc.RootElement
        };

        var service = new TemplateService(
            new Dictionary<string, string> { ["array-min"] = yaml },
            NullLogger<TemplateService>.Instance);

        var ex = await Assert.ThrowsAsync<TemplateValidationException>(() =>
            service.GenerateEngineModelAsync("array-min", parameters));

        Assert.Contains("minimum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateEngineModelAsync_ShouldRejectArrayLengthMismatches()
    {
        const string yaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: array-length
  title: Array Length Validation
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
parameters:
  - name: baseLoad
    type: array
    arrayOf: double
topology:
  nodes:
    - id: service
      kind: service
      semantics:
        arrivals: base_requests
        served: base_requests
  edges: []
nodes:
  - id: base_requests
    kind: const
    values: ${baseLoad}
outputs:
  - id: base_series
    series: base_requests
""";

        using var doc = JsonDocument.Parse("[1.0, 2.0]");
        var parameters = new Dictionary<string, object>
        {
            ["baseLoad"] = doc.RootElement
        };

        var service = new TemplateService(
            new Dictionary<string, string> { ["array-length"] = yaml },
            NullLogger<TemplateService>.Instance);

        var ex = await Assert.ThrowsAsync<TemplateValidationException>(() =>
            service.GenerateEngineModelAsync("array-length", parameters));

        Assert.Contains("length", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
    
    private static string ResolveTemplatePath(string templateFileName)
    {
        var directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(directory))
        {
            var solutionPath = Path.Combine(directory, "FlowTime.sln");
            if (File.Exists(solutionPath))
            {
                var templatePath = Path.Combine(directory, "templates", templateFileName);
                if (File.Exists(templatePath))
                {
                    return templatePath;
                }

                throw new FileNotFoundException($"Template file '{templateFileName}' not found under templates directory.");
            }

            directory = Path.GetDirectoryName(directory) ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Unable to locate templates directory relative to solution root.");
    }
}
