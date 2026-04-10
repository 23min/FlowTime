using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FlowTime.Core.Execution;

/// <summary>
/// Subprocess bridge to the Rust flowtime-engine binary.
/// Writes model YAML to a temp file, invokes <c>flowtime-engine eval</c>,
/// and reads back the structured artifacts (series CSVs, index.json, run.json, manifest.json).
/// </summary>
public sealed class RustEngineRunner
{
    private readonly string enginePath;
    private readonly ILogger? logger;
    private readonly TimeSpan processTimeout;
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Default process timeout: 60 seconds.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    public RustEngineRunner(string enginePath, ILogger? logger = null, TimeSpan? processTimeout = null)
    {
        this.enginePath = enginePath ?? throw new ArgumentNullException(nameof(enginePath));
        this.logger = logger;
        this.processTimeout = processTimeout ?? DefaultTimeout;
    }

    public sealed record RustEvalResult
    {
        public required RustRunMetadata Run { get; init; }
        public required RustManifest? Manifest { get; init; }
        public required IReadOnlyList<RustSeriesResult> Series { get; init; }
    }

    public sealed record RustRunMetadata
    {
        public int SchemaVersion { get; init; }
        public string EngineVersion { get; init; } = string.Empty;
        public RustGridInfo Grid { get; init; } = new();
        public IReadOnlyList<RustWarning> Warnings { get; init; } = [];
        public IReadOnlyList<RustSeriesRef> Series { get; init; } = [];
    }

    public sealed record RustGridInfo
    {
        public int Bins { get; init; }
        public int BinSize { get; init; }
        public string BinUnit { get; init; } = string.Empty;
    }

    public sealed record RustWarning
    {
        public string NodeId { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
    }

    public sealed record RustSeriesRef
    {
        public string Id { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
    }

    public sealed record RustManifest
    {
        public int SchemaVersion { get; init; }
        public string Engine { get; init; } = string.Empty;
        public string EngineVersion { get; init; } = string.Empty;
        public RustGridInfo Grid { get; init; } = new();
        public string? ModelHash { get; init; }
        public IReadOnlyList<RustManifestSeries> Series { get; init; } = [];
    }

    public sealed record RustManifestSeries
    {
        public string Id { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string Hash { get; init; } = string.Empty;
    }

    public sealed record RustSeriesResult
    {
        public required string Id { get; init; }
        public required double[] Values { get; init; }
    }

    /// <summary>
    /// Evaluate a model YAML via the Rust engine subprocess.
    /// </summary>
    public async Task<RustEvalResult> EvaluateAsync(string modelYaml, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelYaml);

        var tempDir = Path.Combine(Path.GetTempPath(), $"flowtime-rust-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var modelPath = Path.Combine(tempDir, "model.yaml");
            var outputDir = Path.Combine(tempDir, "output");

            // Use UTF8 without BOM — serde_yaml rejects BOM as multi-document YAML
            await File.WriteAllTextAsync(modelPath, modelYaml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);

            var (exitCode, stdout, stderr) = await RunProcessAsync(
                enginePath,
                $"eval \"{modelPath}\" --output \"{outputDir}\"",
                cancellationToken);

            if (exitCode != 0)
            {
                throw new RustEngineException(
                    $"flowtime-engine exited with code {exitCode}: {stderr.Trim()}",
                    exitCode,
                    stderr);
            }

            var runJson = Path.Combine(outputDir, "run.json");
            if (!File.Exists(runJson))
            {
                throw new RustEngineException("flowtime-engine did not produce run.json", exitCode, stderr);
            }

            var run = await ReadRunJsonAsync(runJson, cancellationToken);
            var manifest = await ReadManifestJsonAsync(Path.Combine(outputDir, "manifest.json"), cancellationToken);
            var series = await ReadSeriesAsync(outputDir, run.Series, cancellationToken);

            return new RustEvalResult
            {
                Run = run,
                Manifest = manifest,
                Series = series,
            };
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    private async Task<(int exitCode, string stdout, string stderr)> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        logger?.LogDebug("Running: {FileName} {Arguments}", fileName, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process process;
        try
        {
            process = new Process { StartInfo = psi };
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new RustEngineException(
                $"Failed to start flowtime-engine at '{fileName}': {ex.Message}");
        }

        using (process)
        {
            // Combine caller cancellation with our timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(processTimeout);

            try
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

                await process.WaitForExitAsync(timeoutCts.Token);

                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                logger?.LogDebug("Exit code: {ExitCode}, stdout: {Stdout}, stderr: {Stderr}",
                    process.ExitCode, stdout.Length > 200 ? stdout[..200] + "..." : stdout, stderr);

                return (process.ExitCode, stdout, stderr);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout fired, not caller cancellation
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                throw new RustEngineException(
                    $"flowtime-engine timed out after {processTimeout.TotalSeconds:F0}s");
            }
            catch (OperationCanceledException)
            {
                // Caller cancelled — kill the process and rethrow
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                throw;
            }
        }
    }

    private static async Task<RustRunMetadata> ReadRunJsonAsync(string path, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<RustRunMetadata>(json, jsonOptions)
            ?? throw new RustEngineException($"Failed to deserialize {path}");
    }

    private static async Task<RustManifest?> ReadManifestJsonAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<RustManifest>(json, jsonOptions);
    }

    private static async Task<IReadOnlyList<RustSeriesResult>> ReadSeriesAsync(
        string outputDir,
        IReadOnlyList<RustSeriesRef> seriesRefs,
        CancellationToken ct)
    {
        var results = new List<RustSeriesResult>(seriesRefs.Count);

        foreach (var seriesRef in seriesRefs)
        {
            var csvPath = Path.Combine(outputDir, seriesRef.Path);
            if (!File.Exists(csvPath))
            {
                throw new RustEngineException($"Series CSV not found: {seriesRef.Path}");
            }

            var lines = await File.ReadAllLinesAsync(csvPath, ct);
            // Skip header line (bin_index,value)
            var values = lines
                .Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line =>
                {
                    var parts = line.Split(',');
                    return double.Parse(parts[1], CultureInfo.InvariantCulture);
                })
                .ToArray();

            results.Add(new RustSeriesResult { Id = seriesRef.Id, Values = values });
        }

        return results;
    }

    private void CleanupTempDir(string tempDir)
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to clean up temp directory: {TempDir}", tempDir);
        }
    }
}

public sealed class RustEngineException : Exception
{
    public int? ExitCode { get; }
    public string? Stderr { get; }

    public RustEngineException(string message, int? exitCode = null, string? stderr = null)
        : base(message)
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }
}
