using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlowTime.Contracts.Services;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.TimeTravel;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Models;
using YamlDotNet.Serialization;

namespace FlowTime.Generator;

/// <summary>
/// Builds canonical telemetry-mode run artifacts from captured series and a FlowTime-Sim model.
/// </summary>
public sealed class TelemetryBundleBuilder
{
    private readonly IDeserializer yamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task<TelemetryBundleResult> BuildAsync(TelemetryBundleOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var captureDir = ValidateDirectory(options.CaptureDirectory, nameof(options.CaptureDirectory));
        var outputRoot = EnsureDirectory(options.OutputRoot, nameof(options.OutputRoot));
        var modelPath = ValidateFile(options.ModelPath, nameof(options.ModelPath));
        var provenancePath = options.ProvenancePath is null ? null : ValidateFile(options.ProvenancePath, nameof(options.ProvenancePath));

        var telemetryManifest = await ReadTelemetryManifestAsync(captureDir, cancellationToken).ConfigureAwait(false);
        var captureFiles = (telemetryManifest.Files ?? Array.Empty<TelemetryManifestFile>())
            .ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);

        var modelYaml = await File.ReadAllTextAsync(modelPath, cancellationToken).ConfigureAwait(false);
        var normalizedYaml = NormalizeTelemetrySources(modelYaml, captureDir, captureFiles);
        var canonicalModel = ModelService.ParseAndConvert(normalizedYaml);
        var telemetrySeries = await LoadTelemetrySeriesAsync(captureDir, telemetryManifest, canonicalModel, cancellationToken).ConfigureAwait(false);
        var coverageWarnings = ValidateClassSeries(telemetryManifest, telemetrySeries);
        telemetryManifest = telemetryManifest with
        {
            Warnings = MergeWarnings(telemetryManifest.Warnings, coverageWarnings)
        };
        var (grid, _) = ModelParser.ParseModel(canonicalModel);

        var outputDirectory = outputRoot;
        string? explicitRunDirectory = null;
        if (!string.IsNullOrWhiteSpace(options.RunId))
        {
            explicitRunDirectory = Path.Combine(outputRoot, options.RunId);
            if (Directory.Exists(explicitRunDirectory))
            {
                if (!options.Overwrite)
                {
                    throw new InvalidOperationException($"Run directory '{explicitRunDirectory}' already exists. Enable Overwrite to replace it.");
                }
                Directory.Delete(explicitRunDirectory, recursive: true);
            }
            outputDirectory = outputRoot;
        }

        var writeRequest = new RunArtifactWriter.WriteRequest
        {
            Model = canonicalModel,
            Grid = grid,
            Context = telemetrySeries.Totals,
            ClassSeriesOverride = telemetrySeries.ClassSeries.Count == 0
                ? null
                : telemetrySeries.ClassSeries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (IReadOnlyDictionary<string, double[]>)new Dictionary<string, double[]>(kvp.Value, StringComparer.OrdinalIgnoreCase),
                    new NodeIdComparer()),
            SpecText = normalizedYaml,
            RngSeed = null,
            StartTimeBias = null,
            DeterministicRunId = options.DeterministicRunId,
            OutputDirectory = outputDirectory,
            Verbose = false,
            ProvenanceJson = provenancePath is null ? null : await File.ReadAllTextAsync(provenancePath, cancellationToken).ConfigureAwait(false),
            InputHash = options.InputHash,
            TemplateId = options.TemplateId
        };

        var writeResult = await RunArtifactWriter.WriteArtifactsAsync(writeRequest).ConfigureAwait(false);
        var runDir = writeResult.RunDirectory;

        if (explicitRunDirectory is not null &&
            !string.Equals(writeResult.RunDirectory, explicitRunDirectory, StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(writeResult.RunDirectory, explicitRunDirectory);
            runDir = explicitRunDirectory;
        }

        var canonicalModelPath = Path.Combine(runDir, "model", "model.yaml");
        if (!File.Exists(canonicalModelPath))
        {
            throw new InvalidOperationException($"Canonical model not found at '{canonicalModelPath}'.");
        }

        await CopyTelemetryFilesAsync(captureDir, captureFiles, runDir, cancellationToken).ConfigureAwait(false);
        await WriteTelemetryManifestAsync(runDir, telemetryManifest, cancellationToken).ConfigureAwait(false);

        return new TelemetryBundleResult(runDir, Path.GetFileName(runDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), telemetryManifest);
    }

    private static string ValidateDirectory(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"{name} must be provided.", name);
        }

        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
        }

        return fullPath;
    }

    private static string EnsureDirectory(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"{name} must be provided.", name);
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    private static string ValidateFile(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"{name} must be provided.", name);
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {fullPath}", fullPath);
        }

        return fullPath;
    }

    private static async Task<TelemetryManifest> ReadTelemetryManifestAsync(string captureDir, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(captureDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Telemetry manifest not found: {manifestPath}");
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<TelemetryManifest>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken).ConfigureAwait(false);
        return manifest ?? throw new InvalidOperationException($"Telemetry manifest at '{manifestPath}' was empty.");
    }

    private static string NormalizeTelemetrySources(string modelYaml, string captureDir, IReadOnlyDictionary<string, TelemetryManifestFile> captureFiles)
    {
        var normalized = modelYaml;
        foreach (var file in captureFiles.Values)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(captureDir, file.Path));
            var uri = new Uri(absolutePath);
            var absoluteUri = uri.AbsoluteUri;
            var relativeUri = $"file://telemetry/{Path.GetFileName(file.Path)}";

            normalized = normalized.Replace(absoluteUri, relativeUri, StringComparison.OrdinalIgnoreCase)
                                   .Replace($"file://{absolutePath.Replace('\\', '/')}", relativeUri, StringComparison.OrdinalIgnoreCase);
        }

        return normalized;
    }

    private sealed record TelemetrySeriesLoadResult(
        Dictionary<NodeId, double[]> Totals,
        Dictionary<NodeId, Dictionary<string, double[]>> ClassSeries);

    private async Task<TelemetrySeriesLoadResult> LoadTelemetrySeriesAsync(
        string captureDir,
        TelemetryManifest manifest,
        ModelDefinition modelDefinition,
        CancellationToken cancellationToken)
    {
        var topology = modelDefinition.Topology?.Nodes ?? throw new InvalidOperationException("Model topology is required.");
        var nodeById = topology.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
        var seriesByFile = BuildSeriesMapping(modelDefinition);

        var context = new Dictionary<NodeId, double[]>(new NodeIdComparer());
        var classSeries = new Dictionary<NodeId, Dictionary<string, double[]>>(new NodeIdComparer());

        foreach (var file in manifest.Files ?? Array.Empty<TelemetryManifestFile>())
        {
            if (!nodeById.TryGetValue(file.NodeId, out var topologyNode))
            {
                continue;
            }

            var seriesNodeId = ResolveSeriesNodeId(topologyNode, file, seriesByFile);
            if (string.IsNullOrWhiteSpace(seriesNodeId))
            {
                continue;
            }

            var csvPath = Path.Combine(captureDir, file.Path);
            var values = await ReadTelemetrySeriesAsync(csvPath, manifest.Grid.Bins, cancellationToken).ConfigureAwait(false);
            var nodeKey = new NodeId(seriesNodeId);
            context[nodeKey] = values;
            if (!string.IsNullOrWhiteSpace(file.ClassId) &&
                !string.Equals(file.ClassId, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            {
                if (!classSeries.TryGetValue(nodeKey, out var perNode))
                {
                    perNode = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                    classSeries[nodeKey] = perNode;
                }

                perNode[file.ClassId] = values;
            }

            var nodeDefinition = modelDefinition.Nodes.FirstOrDefault(n => string.Equals(n.Id, seriesNodeId, StringComparison.OrdinalIgnoreCase));
            if (nodeDefinition is not null)
            {
                nodeDefinition.Values = values.ToArray();
            }
        }

        return new TelemetrySeriesLoadResult(context, classSeries);
    }

    private static IReadOnlyDictionary<string, string> BuildSeriesMapping(ModelDefinition modelDefinition)
    {
        if (modelDefinition.Outputs is null || modelDefinition.Outputs.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var output in modelDefinition.Outputs)
        {
            if (output is null || string.IsNullOrWhiteSpace(output.As) || string.IsNullOrWhiteSpace(output.Series))
            {
                continue;
            }

            var fileName = Path.GetFileName(output.As);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var seriesId = output.Series!.Trim();
            map[fileName] = seriesId;
        }

        return map;
    }

    private static string? ResolveSeriesNodeId(
        TopologyNodeDefinition node,
        TelemetryManifestFile manifestFile,
        IReadOnlyDictionary<string, string> seriesByFile)
    {
        var semantics = node.Semantics ?? throw new InvalidOperationException($"Node '{node.Id}' is missing semantics.");
        var candidate = GetSemanticsValue(semantics, manifestFile.Metric);
        var resolved = NormalizeSeriesIdentifier(candidate, manifestFile.Path, seriesByFile);
        return resolved;
    }

    private static string? NormalizeSeriesIdentifier(
        string? candidate,
        string manifestPath,
        IReadOnlyDictionary<string, string> seriesByFile)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var trimmed = candidate.Trim();

            if (TryResolveFromMapping(trimmed, seriesByFile, out var mapped))
            {
                return mapped;
            }

            if (!LooksLikeSeriesId(trimmed))
            {
                var fileName = ExtractFileName(trimmed);
                if (!string.IsNullOrWhiteSpace(fileName) &&
                    TryResolveFromMapping(fileName, seriesByFile, out mapped))
                {
                    return mapped;
                }
            }
            else
            {
                return trimmed;
            }
        }

        var manifestFileName = Path.GetFileName(manifestPath);
        if (!string.IsNullOrWhiteSpace(manifestFileName) &&
            TryResolveFromMapping(manifestFileName, seriesByFile, out var manifestMapped))
        {
            return manifestMapped;
        }

        return null;
    }

    private static bool LooksLikeSeriesId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        if (value.Contains('/') || value.Contains('\\'))
        {
            return false;
        }

        if (value.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool TryResolveFromMapping(
        string candidate,
        IReadOnlyDictionary<string, string> seriesByFile,
        out string? resolved)
    {
        if (seriesByFile.TryGetValue(candidate, out resolved))
        {
            return true;
        }

        var fileName = Path.GetFileName(candidate);
        if (!string.IsNullOrWhiteSpace(fileName) && seriesByFile.TryGetValue(fileName, out resolved))
        {
            return true;
        }

        resolved = null;
        return false;
    }

    private static string? ExtractFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && absolute.IsAbsoluteUri)
        {
            return Path.GetFileName(absolute.LocalPath);
        }

        return Path.GetFileName(value);
    }

    private static string? GetSemanticsKey(TelemetryMetricKind metric) => metric switch
    {
        TelemetryMetricKind.Arrivals => "arrivals",
        TelemetryMetricKind.Served => "served",
        TelemetryMetricKind.Errors => "errors",
        TelemetryMetricKind.ExternalDemand => "externalDemand",
        TelemetryMetricKind.QueueDepth => "queueDepth",
        TelemetryMetricKind.Capacity => "capacity",
        _ => null
    };

    private static string? GetSemanticsValue(TopologyNodeSemanticsDefinition semantics, TelemetryMetricKind metric) => metric switch
    {
        TelemetryMetricKind.Arrivals => semantics.Arrivals,
        TelemetryMetricKind.Served => semantics.Served,
        TelemetryMetricKind.Errors => semantics.Errors,
        TelemetryMetricKind.ExternalDemand => semantics.ExternalDemand,
        TelemetryMetricKind.QueueDepth => semantics.QueueDepth,
        TelemetryMetricKind.Capacity => semantics.Capacity,
        _ => null
    };

    private static async Task<double[]> ReadTelemetrySeriesAsync(string path, int expectedPoints, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Telemetry CSV not found: {path}", path);
        }

        var values = new List<double>(expectedPoints);
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var header = await reader.ReadLineAsync().ConfigureAwait(false);
        if (header is null)
        {
            throw new InvalidDataException($"Telemetry CSV {path} is empty.");
        }

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 2)
            {
                throw new InvalidDataException($"Invalid telemetry row '{line}' in {path}.");
            }

            var valueText = parts[^1].Trim();
            if (string.IsNullOrEmpty(valueText))
            {
                values.Add(double.NaN);
            }
            else if (valueText.Equals("NaN", StringComparison.OrdinalIgnoreCase))
            {
                values.Add(double.NaN);
            }
            else if (double.TryParse(valueText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                values.Add(parsed);
            }
            else
            {
                throw new InvalidDataException($"Invalid numeric value '{valueText}' in {path}.");
            }
        }

        if (expectedPoints > 0 && values.Count != expectedPoints)
        {
            throw new InvalidDataException($"Telemetry CSV '{path}' contained {values.Count} points but expected {expectedPoints}.");
        }

        return values.ToArray();
    }

    private static Task CopyTelemetryFilesAsync(
        string captureDir,
        IReadOnlyDictionary<string, TelemetryManifestFile> captureFiles,
        string runDir,
        CancellationToken cancellationToken)
    {
        var telemetryTarget = Path.Combine(runDir, "model", "telemetry");
        Directory.CreateDirectory(telemetryTarget);

        foreach (var manifestEntry in captureFiles.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = Path.Combine(captureDir, manifestEntry.Path);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Telemetry CSV not found: {sourcePath}", sourcePath);
            }

            var destinationPath = Path.Combine(telemetryTarget, Path.GetFileName(manifestEntry.Path));
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        return Task.CompletedTask;
    }

    private static async Task WriteTelemetryManifestAsync(string runDir, TelemetryManifest manifest, CancellationToken cancellationToken)
    {
        var telemetryManifestPath = Path.Combine(runDir, "model", "telemetry", "telemetry-manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(telemetryManifestPath)!);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        await File.WriteAllTextAsync(telemetryManifestPath, json, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<CaptureWarning> MergeWarnings(IReadOnlyList<CaptureWarning>? existing, IReadOnlyList<CaptureWarning>? additional)
    {
        if (additional is null || additional.Count == 0)
        {
            return existing ?? Array.Empty<CaptureWarning>();
        }

        if (existing is null || existing.Count == 0)
        {
            return additional.ToArray();
        }

        var merged = new List<CaptureWarning>(existing.Count + additional.Count);
        merged.AddRange(existing);
        merged.AddRange(additional);
        return merged;
    }

    private static List<CaptureWarning> ValidateClassSeries(TelemetryManifest manifest, TelemetrySeriesLoadResult series)
    {
        var warnings = new List<CaptureWarning>();
        var declaredClasses = manifest.Classes?
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Where(c => !string.Equals(c, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        if (manifest.SupportsClassMetrics)
        {
            if (declaredClasses.Length == 0)
            {
                warnings.Add(new CaptureWarning("class_manifest_missing", "supportsClassMetrics=true but no classes were declared."));
            }

            if (series.ClassSeries.Count == 0)
            {
                warnings.Add(new CaptureWarning("class_data_missing", "Manifest declares class metrics but no class CSVs were found."));
            }
        }

        foreach (var (nodeId, perClass) in series.ClassSeries)
        {
            if (!series.Totals.ContainsKey(nodeId))
            {
                warnings.Add(new CaptureWarning("class_totals_missing", $"Per-class CSVs exist for '{nodeId.Value}' but the totals series was not found.", nodeId.Value));
            }
        }

        foreach (var (nodeId, totals) in series.Totals)
        {
            if (!series.ClassSeries.TryGetValue(nodeId, out var perClass) || perClass.Count == 0)
            {
                if (manifest.SupportsClassMetrics)
                {
                    warnings.Add(new CaptureWarning("class_series_missing", $"Node '{nodeId.Value}' is missing per-class CSVs.", nodeId.Value));
                }
                continue;
            }

            foreach (var required in declaredClasses)
            {
                if (!perClass.ContainsKey(required))
                {
                    warnings.Add(new CaptureWarning("class_series_partial", $"Node '{nodeId.Value}' is missing class '{required}'.", nodeId.Value));
                }
            }

            for (var i = 0; i < totals.Length; i++)
            {
                var total = totals[i];
                if (double.IsNaN(total))
                {
                    continue;
                }

                var sum = 0d;
                var hasSamples = false;
                foreach (var kvp in perClass)
                {
                    var classSeries = kvp.Value;
                    if (i >= classSeries.Length)
                    {
                        continue;
                    }

                    var value = classSeries[i];
                    if (double.IsNaN(value))
                    {
                        continue;
                    }

                    hasSamples = true;
                    sum += value;
                }

                if (!hasSamples)
                {
                    continue;
                }

                var tolerance = Math.Max(1e-6, Math.Abs(total) * 1e-6);
                if (Math.Abs(total - sum) > tolerance)
                {
                    warnings.Add(new CaptureWarning(
                        "class_conservation_mismatch",
                        $"Node '{nodeId.Value}' total differs from class sum (bin {i}, total={total:G4}, sum={sum:G4}).",
                        nodeId.Value,
                        new[] { i }));
                    break;
                }
            }
        }

        return warnings;
    }

    private sealed class NodeIdComparer : IEqualityComparer<NodeId>
    {
        public bool Equals(NodeId x, NodeId y) => string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(NodeId obj) => obj.Value?.ToLowerInvariant().GetHashCode() ?? 0;
    }
}
