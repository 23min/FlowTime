# Tracking: m-E17-01 WebSocket Engine Bridge

**Milestone:** m-E17-01
**Epic:** E-17 Interactive What-If Mode
**Status:** in-progress
**Branch:** `milestone/m-E17-01-websocket-engine-bridge`
**Started:** 2026-04-10

## Progress

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | WebSocket endpoint | pending |
| AC-2 | Engine process lifecycle | pending |
| AC-3 | Bidirectional proxy (binary frames) | pending |
| AC-4 | Multiple concurrent sessions | pending |
| AC-5 | Error handling (crash, missing binary) | pending |
| AC-6 | CORS compatibility | pending |
| AC-7 | Health check endpoint | pending |
| AC-8 | Svelte WebSocket client module | pending |
| AC-9 | C# integration test | pending |
| AC-10 | Svelte smoke test page | pending |

## Implementation Phases

### Phase 1: .NET WebSocket proxy (AC-1, AC-2, AC-3, AC-5, AC-6, AC-7)
- Add app.UseWebSockets()
- EngineSessionBridge service: manages subprocess lifecycle
- WebSocket endpoint: /v1/engine/session
- Health endpoint: /v1/engine/session/health
- Proxy loop: WS↔stdin/stdout binary frames
- Error handling: 1011 on crash, 1013 on missing binary

### Phase 2: C# integration tests (AC-4, AC-9)
- WebApplicationFactory test: connect WS, send compile+eval, verify response
- Concurrent sessions test

### Phase 3: Svelte client module (AC-8)
- ui/src/lib/api/engine-session.ts
- MessagePack encode/decode with length prefix
- Typed async methods: compile, eval, getParams, getSeries
- Reconnection handling

### Phase 4: Svelte smoke test page (AC-10)
- /engine-test route
- Hardcoded simple model
- Display params + one series
