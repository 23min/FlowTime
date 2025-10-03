using System.Globalization;
using System.Text.Json;
using FlowTime.Core;

namespace FlowTime.Adapters.Synthetic;

/// <summary>
/// File-based reader for FlowTime/Sim artifacts
/// Handles missing optional series gracefully and provides deterministic re-export
/// </summary>
public sealed class FileSeriesReader : ISeriesReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<RunManifest> ReadRunInfoAsync(string runPath)
    {
        var runJsonPath = Path.Combine(runPath, "run.json");
        if (!File.Exists(runJsonPath))
        {
            throw new FileNotFoundException($"run.json not found at {runJsonPath}");
        }

        var json = await File.ReadAllTextAsync(runJsonPath);
        var runDoc = JsonDocument.Parse(json);
        var root = runDoc.RootElement;

        return new RunManifest
        {
            SchemaVersion = root.GetProperty("schemaVersion").GetInt32(),
            RunId = root.GetProperty("runId").GetString()!,
            EngineVersion = root.GetProperty("engineVersion").GetString()!,
            Source = root.GetProperty("source").GetString()!,
            Grid = ParseTimeGrid(root.GetProperty("grid")),
            ModelHash = root.TryGetProperty("modelHash", out var modelHashProp) 
                ? modelHashProp.GetString() 
                : null,
            ScenarioHash = root.GetProperty("scenarioHash").GetString()!,
            CreatedUtc = DateTime.Parse(root.GetProperty("createdUtc").GetString()!, 
                null, DateTimeStyles.RoundtripKind),
            Warnings = root.TryGetProperty("warnings", out var warningsProp)
                ? warningsProp.EnumerateArray().Select(w => w.GetString()!).ToArray()
                : [],
            Series = root.GetProperty("series").EnumerateArray()
                .Select(ParseSeriesReference)
                .ToArray()
        };
    }

    public async Task<DeterministicManifest> ReadManifestAsync(string runPath)
    {
        var manifestPath = Path.Combine(runPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"manifest.json not found at {manifestPath}");
        }

        var json = await File.ReadAllTextAsync(manifestPath);
        var manifestDoc = JsonDocument.Parse(json);
        var root = manifestDoc.RootElement;

        return new DeterministicManifest
        {
            SchemaVersion = root.GetProperty("schemaVersion").GetInt32(),
            ScenarioHash = root.GetProperty("scenarioHash").GetString()!,
            Rng = ParseRngInfo(root.GetProperty("rng")),
            SeriesHashes = root.GetProperty("seriesHashes").EnumerateObject()
                .ToDictionary(prop => prop.Name, prop => prop.Value.GetString()!),
            EventCount = root.GetProperty("eventCount").GetInt32(),
            CreatedUtc = DateTime.Parse(root.GetProperty("createdUtc").GetString()!, 
                null, DateTimeStyles.RoundtripKind),
            ModelHash = root.TryGetProperty("modelHash", out var modelHashProp) 
                ? modelHashProp.GetString() 
                : null
        };
    }

    public async Task<SeriesIndex> ReadIndexAsync(string runPath)
    {
        var indexPath = Path.Combine(runPath, "series", "index.json");
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException($"series/index.json not found at {indexPath}");
        }

        var json = await File.ReadAllTextAsync(indexPath);
        var indexDoc = JsonDocument.Parse(json);
        var root = indexDoc.RootElement;

        return new SeriesIndex
        {
            SchemaVersion = root.GetProperty("schemaVersion").GetInt32(),
            Grid = ParseTimeGrid(root.GetProperty("grid")),
            Series = root.GetProperty("series").EnumerateArray()
                .Select(ParseSeriesMetadata)
                .ToArray()
        };
    }

    public async Task<Series> ReadSeriesAsync(string runPath, string seriesId)
    {
        // Series files use filename-safe transformation of seriesId
        var fileName = $"{seriesId}.csv";
        var seriesPath = Path.Combine(runPath, "series", fileName);
        
        if (!File.Exists(seriesPath))
        {
            throw new FileNotFoundException($"Series file not found: {seriesPath}");
        }

        var lines = await File.ReadAllLinesAsync(seriesPath);
        
        // Skip header line (t,value)
        if (lines.Length < 2)
        {
            throw new InvalidDataException($"Series file {seriesPath} must have header and at least one data row");
        }

        var dataLines = lines.Skip(1);
        var values = new List<double>();

        foreach (var line in dataLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            var parts = line.Split(',');
            if (parts.Length != 2)
            {
                throw new InvalidDataException($"Invalid CSV format in {seriesPath}: expected 't,value' but got '{line}'");
            }

            // Parse value using InvariantCulture to match the determinism rules
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidDataException($"Invalid numeric value in {seriesPath}: '{parts[1]}'");
            }

            values.Add(value);
        }

        return new Series(values.ToArray());
    }

    public bool SeriesExists(string runPath, string seriesId)
    {
        var fileName = $"{seriesId}.csv";
        var seriesPath = Path.Combine(runPath, "series", fileName);
        return File.Exists(seriesPath);
    }

    private static TimeGrid ParseTimeGrid(JsonElement gridElement)
    {
        return new TimeGrid(
            Bins: gridElement.GetProperty("bins").GetInt32(),
            BinSize: gridElement.GetProperty("binSize").GetInt32(),
            BinUnit: gridElement.GetProperty("binUnit").GetString()!,
            Timezone: gridElement.TryGetProperty("timezone", out var tzProp) 
                ? tzProp.GetString()! 
                : "UTC",
            Align: gridElement.TryGetProperty("align", out var alignProp) 
                ? alignProp.GetString()! 
                : "left"
        );
    }

    private static RngInfo ParseRngInfo(JsonElement rngElement)
    {
        return new RngInfo
        {
            Kind = rngElement.GetProperty("kind").GetString()!,
            Seed = rngElement.GetProperty("seed").GetInt32()
        };
    }

    private static SeriesReference ParseSeriesReference(JsonElement element)
    {
        return new SeriesReference
        {
            Id = element.GetProperty("id").GetString()!,
            Path = element.GetProperty("path").GetString()!,
            Unit = element.GetProperty("unit").GetString()!
        };
    }

    private static SeriesMetadata ParseSeriesMetadata(JsonElement element)
    {
        return new SeriesMetadata
        {
            Id = element.GetProperty("id").GetString()!,
            Kind = element.GetProperty("kind").GetString()!,
            Path = element.GetProperty("path").GetString()!,
            Unit = element.GetProperty("unit").GetString()!,
            ComponentId = element.GetProperty("componentId").GetString()!,
            Class = element.GetProperty("class").GetString()!,
            Points = element.GetProperty("points").GetInt32(),
            Hash = element.GetProperty("hash").GetString()!
        };
    }
}
