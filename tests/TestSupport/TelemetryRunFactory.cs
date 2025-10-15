using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowTime.Tests.Support;

public static class TelemetryRunFactory
{
    public static string CreateRunArtifacts(string root, string runId, bool includeTopology = true)
    {
        var runDir = Path.Combine(root, runId);
        Directory.CreateDirectory(runDir);
        var seriesDir = Path.Combine(runDir, "series");
        Directory.CreateDirectory(seriesDir);

        var series = new Dictionary<string, double[]>
        {
            ["order_arrivals"] = new[] { 10d, 11d, 12d, 13d },
            ["order_served"] = new[] { 8d, 9d, 10d, 11d },
            ["order_errors"] = new[] { 1d, 1d, 1d, 1d },
            ["payment_arrivals"] = new[] { 5d, 6d, 7d, 8d },
            ["payment_served"] = new[] { 4d, 5d, 6d, 7d },
            ["payment_errors"] = new[] { 0d, 0d, 0d, 0d }
        };

        foreach (var (seriesId, values) in series)
        {
            var fileName = seriesId switch
            {
                "order_arrivals" => "order_arrivals@ORDER_ARRIVALS@DEFAULT.csv",
                "order_served" => "order_served@ORDER_SERVED@DEFAULT.csv",
                "order_errors" => "order_errors@ORDER_ERRORS@DEFAULT.csv",
                "payment_arrivals" => "payment_arrivals@PAYMENT_ARRIVALS@DEFAULT.csv",
                "payment_served" => "payment_served@PAYMENT_SERVED@DEFAULT.csv",
                "payment_errors" => "payment_errors@PAYMENT_ERRORS@DEFAULT.csv",
                _ => throw new InvalidOperationException()
            };

            WriteCanonicalCsv(Path.Combine(seriesDir, fileName), values);
        }

        var spec = BuildSpec(includeTopology);
        File.WriteAllText(Path.Combine(runDir, "spec.yaml"), spec);

        var runJson = new
        {
            schemaVersion = 1,
            runId,
            engineVersion = "0.1.0-test",
            source = "unit-test",
            grid = new
            {
                bins = 4,
                binSize = 5,
                binUnit = "minutes",
                timezone = "UTC",
                align = "left"
            },
            scenarioHash = "sha256:1111111111111111111111111111111111111111111111111111111111111111",
            createdUtc = DateTime.UtcNow.ToString("O"),
            warnings = Array.Empty<string>(),
            series = new object[]
            {
                RunSeries("order_arrivals@ORDER_ARRIVALS@DEFAULT.csv"),
                RunSeries("order_served@ORDER_SERVED@DEFAULT.csv"),
                RunSeries("order_errors@ORDER_ERRORS@DEFAULT.csv"),
                RunSeries("payment_arrivals@PAYMENT_ARRIVALS@DEFAULT.csv"),
                RunSeries("payment_served@PAYMENT_SERVED@DEFAULT.csv"),
                RunSeries("payment_errors@PAYMENT_ERRORS@DEFAULT.csv")
            }
        };

        WriteJson(Path.Combine(runDir, "run.json"), runJson);

        var index = new
        {
            schemaVersion = 1,
            grid = new
            {
                bins = 4,
                binSize = 5,
                binUnit = "minutes",
                timezone = "UTC"
            },
            series = new object[]
            {
                IndexEntry("order_arrivals@ORDER_ARRIVALS@DEFAULT.csv", "ORDER_ARRIVALS"),
                IndexEntry("order_served@ORDER_SERVED@DEFAULT.csv", "ORDER_SERVED"),
                IndexEntry("order_errors@ORDER_ERRORS@DEFAULT.csv", "ORDER_ERRORS"),
                IndexEntry("payment_arrivals@PAYMENT_ARRIVALS@DEFAULT.csv", "PAYMENT_ARRIVALS"),
                IndexEntry("payment_served@PAYMENT_SERVED@DEFAULT.csv", "PAYMENT_SERVED"),
                IndexEntry("payment_errors@PAYMENT_ERRORS@DEFAULT.csv", "PAYMENT_ERRORS"),
            }
        };

        WriteJson(Path.Combine(seriesDir, "index.json"), index);

        return runDir;

        static object RunSeries(string fileName) => new
        {
            id = Path.GetFileNameWithoutExtension(fileName),
            path = $"series/{fileName}",
            unit = "entities/bin"
        };

        static object IndexEntry(string fileName, string componentId) => new
        {
            id = Path.GetFileNameWithoutExtension(fileName),
            kind = "flow",
            path = $"series/{fileName}",
            unit = "entities/bin",
            componentId,
            @class = "DEFAULT",
            points = 4,
            hash = "sha256:placeholder"
        };
    }

    private static string BuildSpec(bool includeTopology)
    {
        var topologyBlock = includeTopology
            ? """
topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: order_arrivals
        served: order_served
        errors: order_errors
    - id: PaymentService
      kind: service
      semantics:
        arrivals: payment_arrivals
        served: payment_served
        errors: payment_errors
  edges: []
"""
            : """
topology:
  nodes: []
  edges: []
""";

        return $"""
schemaVersion: 1
metadata:
  id: order-system
  title: Order System Fixture
  version: 1.0.0
grid:
  bins: 4
  binSize: 5
  binUnit: minutes
  startTimeUtc: "2025-01-01T00:00:00Z"
{topologyBlock}

nodes:
  - id: order_arrivals
    kind: const
    values: [10, 11, 12, 13]
  - id: order_served
    kind: const
    values: [8, 9, 10, 11]
  - id: order_errors
    kind: const
    values: [1, 1, 1, 1]
  - id: payment_arrivals
    kind: const
    values: [5, 6, 7, 8]
  - id: payment_served
    kind: const
    values: [4, 5, 6, 7]
  - id: payment_errors
    kind: const
    values: [0, 0, 0, 0]
outputs:
  - series: order_arrivals
    as: OrderService_arrivals.csv
  - series: order_served
    as: OrderService_served.csv
  - series: order_errors
    as: OrderService_errors.csv
  - series: payment_arrivals
    as: PaymentService_arrivals.csv
  - series: payment_served
    as: PaymentService_served.csv
  - series: payment_errors
    as: PaymentService_errors.csv
""";
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
