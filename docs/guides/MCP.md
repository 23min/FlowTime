# FlowTime MCP (Model Context Protocol) Guide

This repo includes a **local MCP server** that exposes a subset of FlowTime Engine + Sim capabilities as MCP tools.

Today, the MCP server is designed to run as a **stdio subprocess** (spawned by a chat client / agent / VS Code extension). It is **not** yet a long-running HTTP service.

Related architecture background:
- `work/epics/completed/ai/mcp-server-architecture.md`
- `work/epics/completed/ai/mcp-modeling.md`
- `work/epics/completed/ai/mcp-modeling-tools.md`

Tracking (Epic 8):
- `work/epics/completed/ai/M-08.01-mcp-server-poc-log.md` through `work/epics/completed/ai/M-08.05-mcp-edge-metrics-support-log.md`

---

## 1) Prerequisites

- **.NET 9 SDK** (for FlowTime Engine + Sim services)
- **Node.js 20+** and npm (for the MCP server)
- **VS Code** (recommended for the dev loop; tasks are provided)

Ports (defaults):
- Engine API: `http://localhost:8080`
- Sim API: `http://localhost:8090`

---

## 2) What went wrong with `tsx: not found`?

The MCP server’s development command is:

```bash
npm run dev
```

Which runs:

```bash
tsx src/index.ts
```

`tsx` is installed via **npm devDependencies**, so if you see:

```text
sh: 1: tsx: not found
```

…it almost always means **you haven’t installed npm dependencies yet** in `tools/mcp-server`.

Fix:

```bash
cd tools/mcp-server
npm install
```

(Equivalent from repo root: `npm install --prefix tools/mcp-server`.)

---

## 3) One-time setup (local dev)

From the repo root:

```bash
# Install MCP server dependencies (includes tsx)
npm install --prefix tools/mcp-server

# Optional: compile TypeScript output (for production-style start)
npm run build --prefix tools/mcp-server
```

---

## 4) Running the full dev loop (VS Code)

The MCP server depends on both backends:
- **FlowTime Engine API** (imports run bundles)
- **FlowTime Sim Service** (orchestrates template runs + draft workflows)

### Recommended: use VS Code tasks

This repo ships tasks in `.vscode/tasks.json`.

1. Start Engine API: task `start-api` (port 8080)
2. Start Sim API: task `start-sim-api` (port 8090)
3. Start MCP server: task `start-mcp-server`

The MCP task runs in `tools/mcp-server` and sets the default env vars:

- `FLOWTIME_SIM_API_URL=http://localhost:8090/api/v1`
- `FLOWTIME_SIM_DRAFT_API_URL=http://localhost:8090/api/v1`
- `FLOWTIME_API_URL=http://localhost:8080/v1`
- `FLOWTIME_DATA_DIR=<repo>/data`
- `FLOWTIME_TEMPLATES_DIR=<repo>/templates`
- `FLOWTIME_TEMPLATES_DRAFT_DIR=<repo>/templates-draft`
- `MCP_MAX_BINS=1000`

### Manual equivalent (if you don’t want tasks)

In three terminals:

Terminal 1 (Engine API):

```bash
dotnet run --project src/FlowTime.API --urls http://0.0.0.0:8080
```

Terminal 2 (Sim API):

```bash
ASPNETCORE_URLS=http://0.0.0.0:8090 dotnet run --project src/FlowTime.Sim.Service
```

Terminal 3 (MCP server):

```bash
cd tools/mcp-server
FLOWTIME_SIM_API_URL=http://localhost:8090/api/v1 \
FLOWTIME_SIM_DRAFT_API_URL=http://localhost:8090/api/v1 \
FLOWTIME_API_URL=http://localhost:8080/v1 \
MCP_MAX_BINS=1000 \
npm run dev
```

You should see something like:

```text
FlowTime MCP server running on stdio
```

---

## 5) Using it from VS Code (MCP client)

Because this server uses **stdio transport**, your MCP-enabled client must be able to launch it as a subprocess.

A typical launch config looks like:

```json
{
  "command": "node",
  "args": ["--import", "tsx", "src/index.ts"],
  "cwd": "<repo>/tools/mcp-server",
  "env": {
    "FLOWTIME_SIM_API_URL": "http://localhost:8090/api/v1",
    "FLOWTIME_SIM_DRAFT_API_URL": "http://localhost:8090/api/v1",
    "FLOWTIME_API_URL": "http://localhost:8080/v1",
    "MCP_MAX_BINS": "1000"
  }
}
```

Notes:
- The exact JSON shape depends on your MCP client/extension.
- If you prefer a production-style launch, use `npm run build` then run `node dist/index.js`.

---

## 6) Environment variables

The MCP server reads:

- `FLOWTIME_SIM_API_URL`
  - Default: `http://localhost:8090/api/v1`
  - Used for: template listing, orchestration runs, series/profile endpoints

- `FLOWTIME_SIM_DRAFT_API_URL`
  - Default: `FLOWTIME_SIM_API_URL`
  - Used for: draft storage + draft run endpoints

- `FLOWTIME_API_URL`
  - Default: `http://localhost:8080/v1`
  - Used for: importing a run bundle into the engine (`POST /v1/runs`)

- `MCP_MAX_BINS`
  - Default: `1000`
  - Guardrail: rejects template run parameters that request more than this bin count

- `MCP_REQUEST_TIMEOUT_MS` (default 30000)
- `MCP_ORCHESTRATION_TIMEOUT_MS` (default 120000)

---

## 7) Testing (optional)

The MCP server has smoke/integration tests under `tools/mcp-server/tests`.

Run tests:

```bash
npm test --prefix tools/mcp-server
```

Some tests are skipped unless you provide environment variables:

- `MCP_TEST_SIM_API_URL`
- `MCP_TEST_ENGINE_API_URL`

Optional (enables additional tests):
- `MCP_TEST_RUN_ID`, `MCP_TEST_START_BIN`, `MCP_TEST_END_BIN`

Example:

```bash
MCP_TEST_SIM_API_URL=http://localhost:8090/api/v1 \
MCP_TEST_ENGINE_API_URL=http://localhost:8080/v1 \
npm test --prefix tools/mcp-server
```

---

## 8) Troubleshooting

### `tsx: not found`

Cause: dependencies not installed.

Fix:

```bash
npm install --prefix tools/mcp-server
```

### 404s / connection errors calling Sim or Engine

Double-check that your URLs include the correct API prefixes:
- Sim: `http://localhost:8090/api/v1`
- Engine: `http://localhost:8080/v1`

Also confirm services are running:
- Engine health: `GET http://localhost:8080/healthz`
- Sim health: `GET http://localhost:8090/healthz`

### Timeouts

For large templates or slow machines, increase timeouts:

```bash
MCP_ORCHESTRATION_TIMEOUT_MS=240000 MCP_REQUEST_TIMEOUT_MS=60000 npm run dev
```

### “Only simulation mode is supported … PoC”

The current MCP server enforces `mode=simulation` in some tool paths.

---

## 9) Deployment notes (future / cloud)

Today, `tools/mcp-server` runs over **stdio** and is meant to be spawned by a client.

To run it as a **hosted service** in Azure, you’ll likely need one of these patterns:

1) **Remote transport wrapper**
- Implement an HTTP/SSE/WebSocket gateway that translates remote requests to a local stdio MCP process.
- Deploy the gateway + MCP server together (same container/pod) so stdio stays local.

2) **Re-host with a network transport**
- Add a first-class network transport to the MCP server (when appropriate for the chosen MCP client ecosystem).

In either case:
- Treat Sim + Engine endpoints as dependencies (network + auth + retries).
- Add authn/authz (API keys / AAD) before exposing the server publicly.
- Restrict outbound access: the MCP server should only be able to reach FlowTime APIs.

If you need a concrete Azure deployment plan (Container Apps / AKS / App Service), we can write a follow-up doc once the intended MCP transport is decided.
