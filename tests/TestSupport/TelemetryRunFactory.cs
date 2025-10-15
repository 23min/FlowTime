using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowTime.API.Services;
using FlowTime.Core.TimeTravel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlowTime.Tests.Support;

public static class TelemetryRunFactory
{
    private static readonly IReadOnlyDictionary<FixtureKind, FixtureDefinition> Definitions = new Dictionary<FixtureKind, FixtureDefinition>
    {
        [FixtureKind.OrderSystem] = new FixtureDefinition(
            MetadataId: "order-system",
            MetadataTitle: "Order System Fixture",
            Bins: 4,
            BinSize: 5,
            Series: new[]
            {
                new FixtureSeries("order_arrivals", "order_arrivals@ORDER_ARRIVALS@DEFAULT.csv", "OrderService_arrivals.csv", "ORDER_ARRIVALS", new[] { 10d, 11d, 12d, 13d }),
                new FixtureSeries("order_served", "order_served@ORDER_SERVED@DEFAULT.csv", "OrderService_served.csv", "ORDER_SERVED", new[] { 8d, 9d, 10d, 11d }),
                new FixtureSeries("order_errors", "order_errors@ORDER_ERRORS@DEFAULT.csv", "OrderService_errors.csv", "ORDER_ERRORS", new[] { 1d, 1d, 1d, 1d }),
                new FixtureSeries("payment_arrivals", "payment_arrivals@PAYMENT_ARRIVALS@DEFAULT.csv", "PaymentService_arrivals.csv", "PAYMENT_ARRIVALS", new[] { 5d, 6d, 7d, 8d }),
                new FixtureSeries("payment_served", "payment_served@PAYMENT_SERVED@DEFAULT.csv", "PaymentService_served.csv", "PAYMENT_SERVED", new[] { 4d, 5d, 6d, 7d }),
                new FixtureSeries("payment_errors", "payment_errors@PAYMENT_ERRORS@DEFAULT.csv", "PaymentService_errors.csv", "PAYMENT_ERRORS", new[] { 0d, 0d, 0d, 0d })
            },
            Services: new[]
            {
                new FixtureService("OrderService", "order_arrivals", "order_served", "order_errors"),
                new FixtureService("PaymentService", "payment_arrivals", "payment_served", "payment_errors")
            },
            Edges: Array.Empty<FixtureEdge>()),

        [FixtureKind.Microservices] = new FixtureDefinition(
            MetadataId: "microservices-system",
            MetadataTitle: "Microservices Fixture",
            Bins: 4,
            BinSize: 5,
            Series: new[]
            {
                new FixtureSeries("api_gateway_arrivals", "api_gateway_arrivals.csv", "ApiGateway_arrivals.csv", "API_GATEWAY_ARRIVALS", new[] { 50d, 60d, 55d, 65d }),
                new FixtureSeries("api_gateway_served", "api_gateway_served.csv", "ApiGateway_served.csv", "API_GATEWAY_SERVED", new[] { 48d, 58d, 54d, 62d }),
                new FixtureSeries("api_gateway_errors", "api_gateway_errors.csv", "ApiGateway_errors.csv", "API_GATEWAY_ERRORS", new[] { 2d, 2d, 1d, 3d }),
                new FixtureSeries("auth_arrivals", "auth_arrivals.csv", "AuthService_arrivals.csv", "AUTH_SERVICE_ARRIVALS", new[] { 48d, 58d, 54d, 62d }),
                new FixtureSeries("auth_served", "auth_served.csv", "AuthService_served.csv", "AUTH_SERVICE_SERVED", new[] { 45d, 55d, 52d, 60d }),
                new FixtureSeries("auth_errors", "auth_errors.csv", "AuthService_errors.csv", "AUTH_SERVICE_ERRORS", new[] { 1d, 1d, 2d, 1d }),
                new FixtureSeries("inventory_arrivals", "inventory_arrivals.csv", "InventoryService_arrivals.csv", "INVENTORY_SERVICE_ARRIVALS", new[] { 45d, 55d, 52d, 60d }),
                new FixtureSeries("inventory_served", "inventory_served.csv", "InventoryService_served.csv", "INVENTORY_SERVICE_SERVED", new[] { 44d, 53d, 50d, 58d }),
                new FixtureSeries("inventory_errors", "inventory_errors.csv", "InventoryService_errors.csv", "INVENTORY_SERVICE_ERRORS", new[] { 1d, 2d, 2d, 2d })
            },
            Services: new[]
            {
                new FixtureService("API_Gateway", "api_gateway_arrivals", "api_gateway_served", "api_gateway_errors"),
                new FixtureService("AuthService", "auth_arrivals", "auth_served", "auth_errors"),
                new FixtureService("InventoryService", "inventory_arrivals", "inventory_served", "inventory_errors")
            },
            Edges: new[]
            {
                new FixtureEdge("API_Gateway:out", "AuthService:in", "edge1"),
                new FixtureEdge("AuthService:out", "InventoryService:in", "edge2")
            }),

        [FixtureKind.HttpService] = new FixtureDefinition(
            MetadataId: "http-service",
            MetadataTitle: "HTTP Service Fixture",
            Bins: 4,
            BinSize: 15,
            Series: new[]
            {
                new FixtureSeries("http_arrivals", "http_arrivals@HTTP_SERVICE@DEFAULT.csv", "HttpService_arrivals.csv", "HTTP_SERVICE_ARRIVALS", new[] { 30d, 28d, 35d, 32d }),
                new FixtureSeries("http_served", "http_served@HTTP_SERVICE@DEFAULT.csv", "HttpService_served.csv", "HTTP_SERVICE_SERVED", new[] { 29d, 27d, 34d, 31d }),
                new FixtureSeries("http_errors", "http_errors@HTTP_SERVICE@DEFAULT.csv", "HttpService_errors.csv", "HTTP_SERVICE_ERRORS", new[] { 1d, 1d, 1d, 1d })
            },
            Services: new[]
            {
                new FixtureService("HttpService", "http_arrivals", "http_served", "http_errors")
            },
            Edges: Array.Empty<FixtureEdge>())
    };

    public static string CreateRunArtifacts(string root, string runId, bool includeTopology = true)
        => CreateRunArtifacts(root, runId, FixtureKind.OrderSystem, includeTopology);

    public static string CreateRunArtifacts(string root, string runId, FixtureKind kind, bool includeTopology = true)
    {
        var definition = Definitions[kind];

        var runDir = Path.Combine(root, runId);
        Directory.CreateDirectory(runDir);
        var seriesDir = Path.Combine(runDir, "series");
        Directory.CreateDirectory(seriesDir);

        foreach (var series in definition.Series)
        {
            WriteCanonicalCsv(Path.Combine(seriesDir, series.CsvFileName), series.Values);
        }

        var spec = BuildSpec(definition, includeTopology);
        File.WriteAllText(Path.Combine(runDir, "spec.yaml"), spec);

        var runJson = new
        {
            schemaVersion = 1,
            runId,
            engineVersion = "0.1.0-test",
            source = "unit-test",
            grid = new
            {
                bins = definition.Bins,
                binSize = definition.BinSize,
                binUnit = "minutes",
                timezone = "UTC",
                align = "left"
            },
            scenarioHash = "sha256:1111111111111111111111111111111111111111111111111111111111111111",
            createdUtc = DateTime.UtcNow.ToString("O"),
            warnings = Array.Empty<string>(),
            series = definition.Series.Select(s => RunSeries(s.CsvFileName)).ToArray()
        };

        WriteJson(Path.Combine(runDir, "run.json"), runJson);

        var index = new
        {
            schemaVersion = 1,
            grid = new
            {
                bins = definition.Bins,
                binSize = definition.BinSize,
                binUnit = "minutes",
                timezone = "UTC"
            },
            series = definition.Series.Select(s => IndexEntry(s, definition.Bins)).ToArray()
        };

        WriteJson(Path.Combine(seriesDir, "index.json"), index);

        return runDir;

        static object RunSeries(string fileName) => new
        {
            id = Path.GetFileNameWithoutExtension(fileName),
            path = $"series/{fileName}",
            unit = "entities/bin"
        };

        static object IndexEntry(FixtureSeries series, int bins) => new
        {
            id = Path.GetFileNameWithoutExtension(series.CsvFileName),
            kind = "flow",
            path = $"series/{series.CsvFileName}",
            unit = "entities/bin",
            componentId = series.ComponentId,
            @class = "DEFAULT",
            points = bins,
            hash = "sha256:placeholder"
        };
    }

    internal static FixtureDefinition GetDefinition(FixtureKind kind) => Definitions[kind];

    private static string BuildSpec(FixtureDefinition definition, bool includeTopology)
    {
        var topologyBlock = includeTopology ? BuildTopologyBlock(definition) : "topology:\n  nodes: []\n  edges: []";
        var nodesBlock = BuildNodesBlock(definition);
        var outputsBlock = BuildOutputsBlock(definition);

        return $"""
schemaVersion: 1
metadata:
  id: {definition.MetadataId}
  title: {definition.MetadataTitle}
  version: 1.0.0
grid:
  bins: {definition.Bins}
  binSize: {definition.BinSize}
  binUnit: minutes
  startTimeUtc: "2025-01-01T00:00:00Z"
{topologyBlock}

nodes:
{nodesBlock}
outputs:
{outputsBlock}
""";
    }

    private static string BuildTopologyBlock(FixtureDefinition definition)
    {
        if (definition.Services.Count == 0)
        {
            return "topology:\n  nodes: []\n  edges: []";
        }

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("topology:");
        builder.AppendLine("  nodes:");
        foreach (var service in definition.Services)
        {
            builder.AppendLine($"    - id: {service.NodeId}");
            builder.AppendLine("      kind: service");
            builder.AppendLine("      semantics:");
            builder.AppendLine($"        arrivals: {service.Arrivals}");
            builder.AppendLine($"        served: {service.Served}");
            if (!string.IsNullOrWhiteSpace(service.Errors))
            {
                builder.AppendLine($"        errors: {service.Errors}");
            }
            if (!string.IsNullOrWhiteSpace(service.ExternalDemand))
            {
                builder.AppendLine($"        externalDemand: {service.ExternalDemand}");
            }
            if (!string.IsNullOrWhiteSpace(service.QueueDepth))
            {
                builder.AppendLine($"        queueDepth: {service.QueueDepth}");
            }
            if (!string.IsNullOrWhiteSpace(service.Capacity))
            {
                builder.AppendLine($"        capacity: {service.Capacity}");
            }
        }

        if (definition.Edges.Count == 0)
        {
            builder.Append("  edges: []");
        }
        else
        {
            builder.AppendLine("  edges:");
            foreach (var edge in definition.Edges)
            {
                builder.AppendLine("    - id: " + (edge.Id ?? "edge"));
                builder.AppendLine($"      from: {edge.Source}");
                builder.AppendLine($"      to: {edge.Target}");
            }
        }

        return builder.ToString();
    }

    private static string BuildNodesBlock(FixtureDefinition definition)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < definition.Series.Count; i++)
        {
            var series = definition.Series[i];
            builder.AppendLine($"  - id: {series.SeriesId}");
            builder.AppendLine("    kind: const");
            builder.AppendLine($"    values: [{string.Join(", ", series.Values.Select(v => v.ToString("G", CultureInfo.InvariantCulture)))}]");
            if (i < definition.Series.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildOutputsBlock(FixtureDefinition definition)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < definition.Series.Count; i++)
        {
            var series = definition.Series[i];
            builder.AppendLine($"  - series: {series.SeriesId}");
            builder.AppendLine($"    as: {series.OutputFileName}");
            if (i < definition.Series.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void WriteCanonicalCsv(string path, IReadOnlyList<double> values)
    {
        using var writer = new StreamWriter(path);
        writer.NewLine = "\n";
        writer.WriteLine("t,value");
        for (var i = 0; i < values.Count; i++)
        {
            writer.WriteLine(FormattableString.Invariant($"{i},{values[i]:G17}"));
        }
    }

    private static void WriteJson(string path, object payload)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(payload, options);
        File.WriteAllText(path, json);
    }
}

public sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ft_gen_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }
}

public enum FixtureKind
{
    OrderSystem,
    Microservices,
    HttpService
}

internal sealed record FixtureDefinition(
    string MetadataId,
    string MetadataTitle,
    int Bins,
    int BinSize,
    IReadOnlyList<FixtureSeries> Series,
    IReadOnlyList<FixtureService> Services,
    IReadOnlyList<FixtureEdge> Edges);

internal sealed record FixtureSeries(
    string SeriesId,
    string CsvFileName,
    string OutputFileName,
    string ComponentId,
    double[] Values);

internal sealed record FixtureService(
    string NodeId,
    string Arrivals,
    string Served,
    string? Errors = null,
    string? ExternalDemand = null,
    string? QueueDepth = null,
    string? Capacity = null);

internal sealed record FixtureEdge(
    string Source,
    string Target,
    string? Id = null);

public static class TestStateQueryServiceFactory
{
    public static StateQueryService Create(string runDirectory, ILogger<StateQueryService>? logger = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArtifactsDirectory"] = Path.GetDirectoryName(runDirectory) ?? string.Empty
            })
            .Build();

        var manifestReader = new RunManifestReader();
        var modeValidator = new ModeValidator();
        if (logger is null)
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Error));
            logger = loggerFactory.CreateLogger<StateQueryService>();
        }

        return new StateQueryService(configuration, logger, manifestReader, modeValidator);
    }
}
