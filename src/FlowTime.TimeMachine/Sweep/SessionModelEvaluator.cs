using System.Diagnostics;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;

namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// <see cref="IModelEvaluator"/> implementation backed by a persistent Rust engine
/// session subprocess (see <c>engine/cli/src/session.rs</c>).
///
/// <para>
/// The session compiles the model once on the first call and reuses the compiled Plan
/// for all subsequent <see cref="EvaluateAsync"/> calls, sending only parameter override
/// deltas. This removes the per-point compile overhead incurred by
/// <see cref="RustModelEvaluator"/> when used in sweep/sensitivity/goal-seek/optimize
/// loops with many evaluations.
/// </para>
///
/// <para>
/// Scoped to the lifetime of a single analysis run (typically one HTTP request). The
/// subprocess is spawned lazily on the first <see cref="EvaluateAsync"/> call and
/// terminated in <see cref="DisposeAsync"/>. Concurrent calls on a single instance are
/// serialized via an internal semaphore — the session protocol is single-flight.
/// </para>
/// </summary>
public sealed class SessionModelEvaluator : IModelEvaluator, IAsyncDisposable
{
    private static readonly MessagePackSerializerOptions MessagePackOptions =
        MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

    private static readonly TimeSpan DisposeGracefulExitTimeout = TimeSpan.FromSeconds(1);

    private readonly string enginePath;
    private readonly ILogger<SessionModelEvaluator>? logger;
    private readonly SemaphoreSlim mutex = new(1, 1);

    private Process? process;
    private Stream? stdin;
    private Stream? stdout;
    private IReadOnlyList<string>? paramIds;
    private bool disposed;

    public SessionModelEvaluator(string enginePath, ILogger<SessionModelEvaluator>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(enginePath))
        {
            throw new ArgumentException(
                "enginePath must not be null or whitespace.",
                nameof(enginePath));
        }
        this.enginePath = enginePath;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
        string modelYaml,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        await mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (process is null)
            {
                return await CompileAsync(modelYaml, cancellationToken).ConfigureAwait(false);
            }

            return await EvalAsync(modelYaml, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            mutex.Release();
        }
    }

    /// <summary>
    /// First call: spawn the subprocess, send <c>compile</c> with the model YAML, record
    /// the parameter IDs from the response, and return the series from the compile result.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, double[]>> CompileAsync(
        string modelYaml,
        CancellationToken cancellationToken)
    {
        SpawnProcess();

        var request = new Dictionary<string, object>
        {
            ["method"] = "compile",
            ["params"] = new Dictionary<string, object> { ["yaml"] = modelYaml },
        };

        var response = await ExchangeAsync(request, cancellationToken).ConfigureAwait(false);
        var result = ExtractResult(response, "compile");

        // Extract param IDs for future eval override extraction.
        paramIds = ExtractParamIds(result);

        // Extract series and return.
        return ExtractSeries(result);
    }

    /// <summary>
    /// Subsequent calls: read parameter values from the patched YAML via
    /// <see cref="ConstNodeReader"/>, send <c>eval</c> with overrides, return series.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, double[]>> EvalAsync(
        string modelYaml,
        CancellationToken cancellationToken)
    {
        var overrides = BuildOverrides(modelYaml, paramIds!);

        var request = new Dictionary<string, object>
        {
            ["method"] = "eval",
            ["params"] = new Dictionary<string, object> { ["overrides"] = overrides },
        };

        var response = await ExchangeAsync(request, cancellationToken).ConfigureAwait(false);
        var result = ExtractResult(response, "eval");

        return ExtractSeries(result);
    }

    /// <summary>
    /// Build the overrides dictionary by reading the current value of each captured
    /// parameter ID from the patched YAML. Parameters that cannot be read are omitted;
    /// the session will use its compile-time default for those.
    /// </summary>
    internal static Dictionary<string, object> BuildOverrides(
        string modelYaml,
        IReadOnlyList<string> paramIds)
    {
        var overrides = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var id in paramIds)
        {
            var value = ConstNodeReader.ReadValue(modelYaml, id);
            if (value.HasValue)
            {
                overrides[id] = value.Value;
            }
        }
        return overrides;
    }

    private void SpawnProcess()
    {
        var psi = new ProcessStartInfo
        {
            FileName = enginePath,
            Arguments = "session",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var spawned = Process.Start(psi)
            ?? throw new InvalidOperationException(
                $"Failed to start engine session process: {enginePath}");

        process = spawned;
        stdin = spawned.StandardInput.BaseStream;
        stdout = spawned.StandardOutput.BaseStream;

        // Drain stderr asynchronously so the subprocess does not block on a full pipe.
        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await spawned.StandardError.ReadLineAsync().ConfigureAwait(false)) is not null)
                {
                    logger?.LogDebug("[engine] {Line}", line);
                }
            }
            catch
            {
                // Process ended — ignore.
            }
        });

        logger?.LogDebug("Engine session spawned (PID {Pid})", spawned.Id);
    }

    /// <summary>
    /// Send a request and read the response. Caller must hold <see cref="mutex"/>.
    /// </summary>
    private async Task<Dictionary<object, object>> ExchangeAsync(
        object request,
        CancellationToken cancellationToken)
    {
        if (stdin is null || stdout is null)
        {
            throw new InvalidOperationException("Session not initialized.");
        }

        await WriteFrameAsync(stdin, request, cancellationToken).ConfigureAwait(false);
        return await ReadFrameAsync(stdout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Serialize <paramref name="payload"/> as MessagePack, prepend a 4-byte big-endian
    /// length prefix, and write the framed message to <paramref name="stream"/>.
    /// </summary>
    internal static async Task WriteFrameAsync(
        Stream stream,
        object payload,
        CancellationToken cancellationToken)
    {
        var bytes = MessagePackSerializer.Serialize(payload, MessagePackOptions);
        var len = bytes.Length;
        var frame = new byte[4 + len];
        frame[0] = (byte)((len >> 24) & 0xff);
        frame[1] = (byte)((len >> 16) & 0xff);
        frame[2] = (byte)((len >> 8) & 0xff);
        frame[3] = (byte)(len & 0xff);
        Buffer.BlockCopy(bytes, 0, frame, 4, len);

        await stream.WriteAsync(frame.AsMemory(0, frame.Length), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Read a 4-byte big-endian length prefix and the subsequent MessagePack payload
    /// from <paramref name="stream"/>, then deserialize.
    /// </summary>
    internal static async Task<Dictionary<object, object>> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var lenBuf = new byte[4];
        await ReadExactAsync(stream, lenBuf, 0, 4, cancellationToken).ConfigureAwait(false);
        var len = (lenBuf[0] << 24) | (lenBuf[1] << 16) | (lenBuf[2] << 8) | lenBuf[3];
        if (len <= 0 || len > 64 * 1024 * 1024)
        {
            throw new InvalidOperationException($"Invalid response frame length: {len}");
        }

        var payload = new byte[len];
        await ReadExactAsync(stream, payload, 0, len, cancellationToken).ConfigureAwait(false);

        return MessagePackSerializer.Deserialize<Dictionary<object, object>>(payload, MessagePackOptions);
    }

    internal static async Task ReadExactAsync(
        Stream stream,
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < count)
        {
            var n = await stream.ReadAsync(
                buffer.AsMemory(offset + read, count - read),
                cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                throw new EndOfStreamException(
                    "Engine session stream closed before all expected bytes were read.");
            }
            read += n;
        }
    }

    /// <summary>
    /// Navigate a response envelope: return the <c>result</c> map on success, or raise
    /// <see cref="InvalidOperationException"/> with the error code + message on failure.
    /// </summary>
    internal static Dictionary<object, object> ExtractResult(
        Dictionary<object, object> response,
        string method)
    {
        if (response.TryGetValue("error", out var errRaw) && errRaw is Dictionary<object, object> err)
        {
            var code = err.TryGetValue("code", out var codeRaw) ? codeRaw?.ToString() ?? "" : "";
            var message = err.TryGetValue("message", out var msgRaw) ? msgRaw?.ToString() ?? "" : "";
            throw new InvalidOperationException(
                $"Engine session returned error on {method}: [{code}] {message}");
        }

        if (!response.TryGetValue("result", out var resultRaw) || resultRaw is not Dictionary<object, object> result)
        {
            throw new InvalidOperationException(
                $"Engine session {method} response missing 'result' field.");
        }

        return result;
    }

    /// <summary>
    /// Extract the <c>params</c> array from a compile result and return the list of
    /// parameter IDs. The session may return zero params for models without const nodes
    /// of the patched kind; in that case the list is empty.
    /// </summary>
    internal static IReadOnlyList<string> ExtractParamIds(Dictionary<object, object> compileResult)
    {
        if (!compileResult.TryGetValue("params", out var paramsRaw) || paramsRaw is not object[] paramsArr)
        {
            return Array.Empty<string>();
        }

        var ids = new List<string>(paramsArr.Length);
        foreach (var item in paramsArr)
        {
            if (item is Dictionary<object, object> p
                && p.TryGetValue("id", out var idRaw)
                && idRaw is string id)
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>
    /// Extract the <c>series</c> map from a result payload. Values arrive as
    /// <c>object[]</c> of boxed doubles; convert to <c>double[]</c> per series.
    /// </summary>
    internal static IReadOnlyDictionary<string, double[]> ExtractSeries(
        Dictionary<object, object> result)
    {
        if (!result.TryGetValue("series", out var seriesRaw) || seriesRaw is not Dictionary<object, object> series)
        {
            throw new InvalidOperationException("Engine session result missing 'series' field.");
        }

        var output = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (keyRaw, valueRaw) in series)
        {
            if (keyRaw is not string key) continue;
            if (valueRaw is not object[] values) continue;

            var converted = new double[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                converted[i] = Convert.ToDouble(values[i], System.Globalization.CultureInfo.InvariantCulture);
            }
            output[key] = converted;
        }
        return output;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;

        if (process is null)
        {
            mutex.Dispose();
            return;
        }

        // Close stdin so the subprocess exits cleanly.
        try { stdin?.Close(); } catch { /* best effort */ }

        // Wait briefly for graceful exit, then kill if still alive.
        try
        {
            using var cts = new CancellationTokenSource(DisposeGracefulExitTimeout);
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
        }
        catch
        {
            // Other exceptions while waiting — fall through to dispose.
        }

        try { process.Dispose(); } catch { /* best effort */ }
        process = null;
        stdin = null;
        stdout = null;
        mutex.Dispose();
    }
}
