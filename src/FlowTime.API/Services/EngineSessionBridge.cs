using System.Diagnostics;
using System.Net.WebSockets;

namespace FlowTime.API.Services;

/// <summary>
/// Resolves the Rust engine binary path and manages session subprocess lifecycle.
/// </summary>
public sealed class EngineSessionBridge
{
    private readonly string enginePath;
    private readonly ILogger<EngineSessionBridge> logger;

    public EngineSessionBridge(IConfiguration configuration, ILogger<EngineSessionBridge> logger)
    {
        this.logger = logger;

        var configured = configuration["RustEngine:BinaryPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            enginePath = configured;
        }
        else
        {
            var solutionRoot = FlowTime.Core.Configuration.DirectoryProvider.FindSolutionRoot();
            enginePath = solutionRoot is not null
                ? Path.Combine(solutionRoot, "engine", "target", "release", "flowtime-engine")
                : "flowtime-engine";
        }
    }

    /// <summary>
    /// True if the engine binary exists and can be executed.
    /// </summary>
    public bool IsAvailable => File.Exists(enginePath);

    public string EnginePath => enginePath;

    /// <summary>
    /// Spawn a session subprocess and proxy WebSocket frames ↔ stdin/stdout.
    /// The subprocess is killed when the WebSocket closes.
    /// </summary>
    public async Task ProxyAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            logger.LogError("Engine binary not found at {Path}", enginePath);
            await CloseWithCode(webSocket, WebSocketCloseStatus.EndpointUnavailable, "engine_unavailable");
            return;
        }

        Process? process = null;
        try
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
            process = Process.Start(psi);
            if (process is null)
            {
                logger.LogError("Failed to start engine session process");
                await CloseWithCode(webSocket, WebSocketCloseStatus.InternalServerError, "spawn_failed");
                return;
            }

            logger.LogDebug("Engine session spawned (PID {Pid})", process.Id);

            // Drain stderr to the logger (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    string? line;
                    while ((line = await process.StandardError.ReadLineAsync()) is not null)
                    {
                        logger.LogDebug("[engine] {Line}", line);
                    }
                }
                catch { /* process ended */ }
            });

            // Two-way proxy
            var stdinStream = process.StandardInput.BaseStream;
            var stdoutStream = process.StandardOutput.BaseStream;

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var wsToStdin = PumpWsToStdin(webSocket, stdinStream, linkedCts.Token);
            var stdoutToWs = PumpStdoutToWs(stdoutStream, webSocket, linkedCts.Token);

            // Wait for either side to end
            var completed = await Task.WhenAny(wsToStdin, stdoutToWs);

            // Cancel the other pump
            linkedCts.Cancel();

            // Close stdin to let the engine exit cleanly
            try { stdinStream.Close(); } catch { }

            // Echo close frame back to client
            if (webSocket.State == WebSocketState.Open)
            {
                var (status, reason) = completed == stdoutToWs
                    ? (WebSocketCloseStatus.InternalServerError, "engine_exited")
                    : (WebSocketCloseStatus.NormalClosure, "client_closed");
                if (completed == stdoutToWs)
                {
                    logger.LogWarning("Engine session exited unexpectedly");
                }
                await CloseWithCode(webSocket, status, reason);
            }
            else if (webSocket.State == WebSocketState.CloseReceived)
            {
                // Client sent close — echo it back
                try
                {
                    await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ok", CancellationToken.None);
                }
                catch { }
            }

            // Swallow any exceptions from the cancelled pump
            try { await Task.WhenAll(wsToStdin, stdoutToWs); } catch { }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Engine session bridge error");
            if (webSocket.State == WebSocketState.Open)
            {
                await CloseWithCode(webSocket, WebSocketCloseStatus.InternalServerError, "bridge_error");
            }
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }
            process?.Dispose();
        }
    }

    /// <summary>
    /// Read binary frames from WebSocket and write to stdin.
    /// Preserves length-prefixed MessagePack framing (transparent proxy).
    /// </summary>
    private static async Task PumpWsToStdin(WebSocket webSocket, Stream stdin, CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await webSocket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Reject text frames — protocol is binary MessagePack only
                    await CloseWithCode(webSocket, WebSocketCloseStatus.InvalidMessageType, "text_frames_not_supported");
                    return;
                }
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var bytes = ms.ToArray();
            if (bytes.Length > 0)
            {
                await stdin.WriteAsync(bytes, ct);
                await stdin.FlushAsync(ct);
            }
        }
    }

    /// <summary>
    /// Read length-prefixed messages from engine stdout and send as WebSocket binary frames.
    /// Each MessagePack message (length prefix + payload) is sent as one WebSocket message.
    /// </summary>
    private static async Task PumpStdoutToWs(Stream stdout, WebSocket webSocket, CancellationToken ct)
    {
        var lenBuf = new byte[4];
        while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            // Read 4-byte big-endian length prefix
            int read = 0;
            while (read < 4)
            {
                var n = await stdout.ReadAsync(lenBuf.AsMemory(read, 4 - read), ct);
                if (n == 0) return; // EOF
                read += n;
            }
            var len = (lenBuf[0] << 24) | (lenBuf[1] << 16) | (lenBuf[2] << 8) | lenBuf[3];
            if (len <= 0 || len > 64 * 1024 * 1024) return;

            // Read payload
            var payload = new byte[4 + len];
            Array.Copy(lenBuf, payload, 4);
            int payloadRead = 0;
            while (payloadRead < len)
            {
                var n = await stdout.ReadAsync(payload.AsMemory(4 + payloadRead, len - payloadRead), ct);
                if (n == 0) return; // EOF mid-message
                payloadRead += n;
            }

            // Send as one WebSocket binary frame (length prefix + payload)
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, ct);
            }
        }
    }

    private static async Task CloseWithCode(WebSocket ws, WebSocketCloseStatus status, string description)
    {
        try
        {
            await ws.CloseAsync(status, description, CancellationToken.None);
        }
        catch { /* best-effort */ }
    }
}
