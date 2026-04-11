# Milestone: WebSocket Engine Bridge

**ID:** m-E17-01
**Epic:** E-17 Interactive What-If Mode
**Status:** complete
**Branch:** `milestone/m-E17-01-websocket-engine-bridge` (merged to main)
**Depends on:** m-E18-02 (engine session + MessagePack protocol)

## Goal

The .NET API exposes a WebSocket endpoint that proxies a client connection to a Rust engine session subprocess. The Svelte UI can connect, send compile/eval commands, and receive streaming results. This is the bridge between the browser and the headless engine.

## Context

After m-E18-02, the Rust engine has a `session` CLI mode: persistent process, MessagePack over stdin/stdout, commands for compile/eval/get_params/get_series. But there's no way for a browser to connect to it — browsers speak WebSocket, not stdin/stdout.

The bridge is deliberately simple: the .NET API is a **transparent proxy**. It spawns one engine session process per WebSocket connection and pipes frames between the two. No protocol translation — the same MessagePack messages flow through. The API adds session lifecycle management (spawn on connect, kill on disconnect) but no business logic.

### Why proxy through .NET instead of direct WebSocket from Rust

- The .NET API already runs, handles CORS, auth, logging, and health checks
- Adding a WebSocket server to the Rust engine would require `tokio`/async runtime — heavyweight for a CLI tool
- The proxy pattern keeps the engine as a pure stdin/stdout component, composable in pipelines
- Future: the API can add session pooling, auth, and rate limiting without changing the engine

## Acceptance Criteria

1. **AC-1: WebSocket endpoint.** `ws://localhost:8081/v1/engine/session` accepts WebSocket upgrade requests. Responds with 101 Switching Protocols on success.

2. **AC-2: Engine process lifecycle.** On WebSocket connect, the API spawns `flowtime-engine session` as a subprocess. On WebSocket close (client disconnect or error), the subprocess is killed and resources cleaned up.

3. **AC-3: Bidirectional proxy.** WebSocket binary frames are forwarded to the engine's stdin. Engine stdout data is forwarded as WebSocket binary frames. The MessagePack length-prefix framing is preserved end-to-end — the API does not parse or modify the payload.

4. **AC-4: Multiple concurrent sessions.** Each WebSocket connection gets its own engine subprocess. Two clients can run independent sessions simultaneously.

5. **AC-5: Error handling.** If the engine process crashes or exits unexpectedly, the WebSocket is closed with a 1011 (Internal Error) status code. If the engine binary is not found, the WebSocket is closed with 1013 (Try Again Later) and an error is logged.

6. **AC-6: CORS compatibility.** The existing permissive CORS policy allows WebSocket connections from the Svelte UI origin (localhost:5173).

7. **AC-7: Health check.** `GET /v1/engine/session/health` returns 200 with `{ "available": true/false }` indicating whether the engine binary is found and executable.

8. **AC-8: Svelte WebSocket client.** A TypeScript client module in `ui/src/lib/api/engine-session.ts` that:
   - Connects to the WebSocket endpoint
   - Encodes requests as length-prefixed MessagePack
   - Decodes responses from length-prefixed MessagePack
   - Exposes typed async methods: `compile(yaml)`, `eval(overrides)`, `getParams()`, `getSeries(names?)`
   - Handles reconnection on disconnect

9. **AC-9: Integration test.** A C# integration test that:
   - Starts the API with WebApplicationFactory
   - Connects a WebSocket client
   - Sends compile + eval sequence via MessagePack
   - Verifies correct series values in responses
   - Verifies WebSocket closes cleanly

10. **AC-10: Svelte smoke test.** A minimal Svelte page at `/engine-test` that connects to the WebSocket, compiles a hardcoded model, displays the parameter list, and shows one series as numbers. This proves the end-to-end path works before m-E17-02 builds the real UI.

## Technical Notes

### .NET WebSocket middleware

```csharp
app.Map("/v1/engine/session", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }
    var ws = await context.WebSockets.AcceptWebSocketAsync();
    await ProxyToEngineSession(ws, enginePath, cancellationToken);
});
```

Requires `app.UseWebSockets()` in the middleware pipeline.

### Proxy implementation

The proxy runs two concurrent tasks:
1. **WS → stdin**: Read WebSocket binary frames, write to engine stdin
2. **stdout → WS**: Read engine stdout, write as WebSocket binary frames

Both tasks run until either side closes. Use `Task.WhenAny` to detect the first close and clean up both sides.

### MessagePack in TypeScript

Use `@msgpack/msgpack` npm package:
```typescript
import { encode, decode } from '@msgpack/msgpack';

// Encode request with length prefix
function encodeMessage(obj: unknown): Uint8Array {
    const payload = encode(obj);
    const frame = new Uint8Array(4 + payload.length);
    new DataView(frame.buffer).setUint32(0, payload.length);
    frame.set(payload, 4);
    return frame;
}
```

### Engine binary path

Reuse the existing `RustEngine:BinaryPath` configuration from Program.cs. Fall back to `engine/target/release/flowtime-engine` relative to solution root.

## Out of Scope

- Parameter UI controls (m-E17-02)
- Chart/topology reactive updates (m-E17-03)
- Session pooling or sharing (multiple clients per session)
- Authentication or authorization
- Binary frame compression

## Key References

- `engine/cli/src/session.rs` — engine session loop
- `engine/cli/src/protocol.rs` — MessagePack framing
- `src/FlowTime.API/Program.cs` — API startup and middleware
- `ui/src/lib/api/client.ts` — existing Svelte HTTP client pattern
- `docs/architecture/headless-engine-architecture.md` — protocol design
