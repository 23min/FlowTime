using System.Net.WebSockets;
using MessagePack;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowTime.Integration.Tests;

/// <summary>
/// Integration tests for the /v1/engine/session WebSocket bridge.
/// Spawns the API via WebApplicationFactory and exercises the MessagePack protocol
/// through the WebSocket proxy.
/// </summary>
public class EngineSessionWebSocketTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public EngineSessionWebSocketTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    private const string SimpleModel = """
        grid:
          bins: 4
          binSize: 1
          binUnit: hours
        nodes:
          - id: arrivals
            kind: const
            values: [10, 10, 10, 10]
          - id: served
            kind: expr
            expr: "arrivals * 0.5"
        """;

    /// <summary>
    /// Encode a request object as MessagePack with a 4-byte big-endian length prefix.
    /// </summary>
    private static byte[] EncodeFrame(object request)
    {
        var payload = MessagePackSerializer.Serialize(request,
            MessagePackSerializerOptions.Standard.WithResolver(
                MessagePack.Resolvers.ContractlessStandardResolver.Instance));

        var frame = new byte[4 + payload.Length];
        var len = payload.Length;
        frame[0] = (byte)((len >> 24) & 0xff);
        frame[1] = (byte)((len >> 16) & 0xff);
        frame[2] = (byte)((len >> 8) & 0xff);
        frame[3] = (byte)(len & 0xff);
        Array.Copy(payload, 0, frame, 4, payload.Length);
        return frame;
    }

    /// <summary>
    /// Receive a length-prefixed MessagePack frame from the WebSocket and decode it.
    /// </summary>
    private static async Task<Dictionary<object, object>> ReceiveFrameAsync(WebSocket ws)
    {
        var buffer = new byte[64 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        var data = ms.ToArray();
        // Strip 4-byte length prefix
        if (data.Length < 4) throw new InvalidOperationException("Frame too short");
        var payload = data.AsMemory(4).ToArray();

        return MessagePackSerializer.Deserialize<Dictionary<object, object>>(payload,
            MessagePackSerializerOptions.Standard.WithResolver(
                MessagePack.Resolvers.ContractlessStandardResolver.Instance));
    }

    private async Task<WebSocket> ConnectAsync()
    {
        var wsClient = factory.Server.CreateWebSocketClient();
        var wsUri = new Uri(factory.Server.BaseAddress, "/v1/engine/session");
        return await wsClient.ConnectAsync(wsUri, CancellationToken.None);
    }

    [Fact]
    public async Task Health_ReturnsAvailability()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/engine/session/health");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("available", json);
    }

    [Fact]
    public async Task WebSocket_CompileAndEval_ReturnsCorrectSeries()
    {
        // Skip if engine binary not available
        using var probe = factory.CreateClient();
        var health = await probe.GetAsync("/v1/engine/session/health");
        var healthJson = await health.Content.ReadAsStringAsync();
        if (!healthJson.Contains("\"available\":true", StringComparison.OrdinalIgnoreCase))
            return;

        using var ws = await ConnectAsync();

        // 1. Compile
        var compileReq = new Dictionary<string, object>
        {
            ["method"] = "compile",
            ["params"] = new Dictionary<string, object> { ["yaml"] = SimpleModel },
        };
        await ws.SendAsync(EncodeFrame(compileReq), WebSocketMessageType.Binary, true, CancellationToken.None);

        var resp1 = await ReceiveFrameAsync(ws);
        Assert.True(resp1.ContainsKey("result"), $"compile should succeed. Response: {string.Join(",", resp1.Keys)}");

        // 2. Eval with override
        var evalReq = new Dictionary<string, object>
        {
            ["method"] = "eval",
            ["params"] = new Dictionary<string, object>
            {
                ["overrides"] = new Dictionary<string, object> { ["arrivals"] = 20.0 },
            },
        };
        await ws.SendAsync(EncodeFrame(evalReq), WebSocketMessageType.Binary, true, CancellationToken.None);

        var resp2 = await ReceiveFrameAsync(ws);
        Assert.True(resp2.ContainsKey("result"), $"eval should succeed. Response: {string.Join(",", resp2.Keys)}");

        // Navigate result → series → served
        var result = (Dictionary<object, object>)resp2["result"];
        var series = (Dictionary<object, object>)result["series"];
        var served = (object[])series["served"];
        // served = arrivals * 0.5 = 20 * 0.5 = 10
        Assert.Equal(4, served.Length);
        foreach (var v in served)
        {
            var num = Convert.ToDouble(v);
            Assert.Equal(10.0, num, precision: 10);
        }

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    [Fact]
    public async Task WebSocket_ConcurrentSessions_AreIndependent()
    {
        using var probe = factory.CreateClient();
        var health = await probe.GetAsync("/v1/engine/session/health");
        var healthJson = await health.Content.ReadAsStringAsync();
        if (!healthJson.Contains("\"available\":true", StringComparison.OrdinalIgnoreCase))
            return;

        // Two concurrent sessions
        using var ws1 = await ConnectAsync();
        using var ws2 = await ConnectAsync();

        // Session 1: compile and eval with arrivals=10 (default)
        var compileReq = new Dictionary<string, object>
        {
            ["method"] = "compile",
            ["params"] = new Dictionary<string, object> { ["yaml"] = SimpleModel },
        };
        await ws1.SendAsync(EncodeFrame(compileReq), WebSocketMessageType.Binary, true, CancellationToken.None);
        await ws2.SendAsync(EncodeFrame(compileReq), WebSocketMessageType.Binary, true, CancellationToken.None);

        _ = await ReceiveFrameAsync(ws1);
        _ = await ReceiveFrameAsync(ws2);

        // Session 1: eval with arrivals=100
        var eval1 = new Dictionary<string, object>
        {
            ["method"] = "eval",
            ["params"] = new Dictionary<string, object>
            {
                ["overrides"] = new Dictionary<string, object> { ["arrivals"] = 100.0 },
            },
        };
        // Session 2: eval with arrivals=4
        var eval2 = new Dictionary<string, object>
        {
            ["method"] = "eval",
            ["params"] = new Dictionary<string, object>
            {
                ["overrides"] = new Dictionary<string, object> { ["arrivals"] = 4.0 },
            },
        };

        await ws1.SendAsync(EncodeFrame(eval1), WebSocketMessageType.Binary, true, CancellationToken.None);
        await ws2.SendAsync(EncodeFrame(eval2), WebSocketMessageType.Binary, true, CancellationToken.None);

        var r1 = await ReceiveFrameAsync(ws1);
        var r2 = await ReceiveFrameAsync(ws2);

        var served1 = (object[])((Dictionary<object, object>)((Dictionary<object, object>)r1["result"])["series"])["served"];
        var served2 = (object[])((Dictionary<object, object>)((Dictionary<object, object>)r2["result"])["series"])["served"];

        // Each session has its own overrides — 100*0.5=50 vs 4*0.5=2
        Assert.Equal(50.0, Convert.ToDouble(served1[0]), precision: 10);
        Assert.Equal(2.0, Convert.ToDouble(served2[0]), precision: 10);

        await ws1.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        await ws2.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    [Fact]
    public async Task WebSocket_TextFrame_IsRejected()
    {
        using var probe = factory.CreateClient();
        var health = await probe.GetAsync("/v1/engine/session/health");
        var healthJson = await health.Content.ReadAsStringAsync();
        if (!healthJson.Contains("\"available\":true", StringComparison.OrdinalIgnoreCase))
            return;

        using var ws = await ConnectAsync();

        // Send a text frame instead of binary — should be rejected
        var textBytes = System.Text.Encoding.UTF8.GetBytes("hello");
        await ws.SendAsync(textBytes, WebSocketMessageType.Text, true, CancellationToken.None);

        // Expect a close frame with InvalidMessageType
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(WebSocketCloseStatus.InvalidMessageType, ws.CloseStatus);
    }

    [Fact]
    public async Task WebSocket_Disconnect_KillsEngineProcess()
    {
        using var probe = factory.CreateClient();
        var health = await probe.GetAsync("/v1/engine/session/health");
        var healthJson = await health.Content.ReadAsStringAsync();
        if (!healthJson.Contains("\"available\":true", StringComparison.OrdinalIgnoreCase))
            return;

        var before = CountEngineProcesses();

        // Open, compile, close — engine process should be cleaned up
        using (var ws = await ConnectAsync())
        {
            var compileReq = new Dictionary<string, object>
            {
                ["method"] = "compile",
                ["params"] = new Dictionary<string, object> { ["yaml"] = SimpleModel },
            };
            await ws.SendAsync(EncodeFrame(compileReq), WebSocketMessageType.Binary, true, CancellationToken.None);
            _ = await ReceiveFrameAsync(ws);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        }

        // Give the server time to reap the subprocess
        await Task.Delay(500);

        var after = CountEngineProcesses();

        // The number of engine processes should not have grown
        Assert.True(after <= before,
            $"Engine process leaked: before={before}, after={after}");
    }

    /// <summary>
    /// Count running flowtime-engine subprocess instances (excluding the test binary itself).
    /// </summary>
    private static int CountEngineProcesses()
    {
        try
        {
            var all = System.Diagnostics.Process.GetProcessesByName("flowtime-engine");
            var count = all.Length;
            foreach (var p in all) p.Dispose();
            return count;
        }
        catch
        {
            return 0;
        }
    }

    [Fact]
    public async Task WebSocket_MissingBinary_ClosesWithEndpointUnavailable()
    {
        // Override config to point at a non-existent binary
        var badFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RustEngine:BinaryPath", "/nonexistent/path/to/engine");
        });

        // Health should report unavailable
        using var client = badFactory.CreateClient();
        var health = await client.GetAsync("/v1/engine/session/health");
        var healthJson = await health.Content.ReadAsStringAsync();
        Assert.Contains("\"available\":false", healthJson, StringComparison.OrdinalIgnoreCase);

        // WebSocket connect should succeed (101) but immediately close
        var wsClient = badFactory.Server.CreateWebSocketClient();
        var wsUri = new Uri(badFactory.Server.BaseAddress, "/v1/engine/session");
        using var ws = await wsClient.ConnectAsync(wsUri, CancellationToken.None);

        // Receive close frame
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, ws.CloseStatus);
    }

    [Fact]
    public async Task WebSocket_ErrorResponse_DoesNotCloseSession()
    {
        using var probe = factory.CreateClient();
        var health = await probe.GetAsync("/v1/engine/session/health");
        var healthJson = await health.Content.ReadAsStringAsync();
        if (!healthJson.Contains("\"available\":true", StringComparison.OrdinalIgnoreCase))
            return;

        using var ws = await ConnectAsync();

        // Send eval before compile → error
        var evalReq = new Dictionary<string, object>
        {
            ["method"] = "eval",
            ["params"] = new Dictionary<string, object>(),
        };
        await ws.SendAsync(EncodeFrame(evalReq), WebSocketMessageType.Binary, true, CancellationToken.None);

        var errResp = await ReceiveFrameAsync(ws);
        Assert.True(errResp.ContainsKey("error"), "should return error");

        // Session should still be alive — compile should work
        var compileReq = new Dictionary<string, object>
        {
            ["method"] = "compile",
            ["params"] = new Dictionary<string, object> { ["yaml"] = SimpleModel },
        };
        await ws.SendAsync(EncodeFrame(compileReq), WebSocketMessageType.Binary, true, CancellationToken.None);

        var okResp = await ReceiveFrameAsync(ws);
        Assert.True(okResp.ContainsKey("result"), "session should recover after error");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }
}
