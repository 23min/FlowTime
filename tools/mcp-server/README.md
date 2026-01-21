# FlowTime MCP Server (PoC)

This package provides a minimal MCP server for FlowTime. It exposes a small tool set for running templates and inspecting runs over MCP (stdio transport).

## Requirements

- Node.js 20+
- FlowTime.Sim.Service reachable over HTTP
- FlowTime API reachable over HTTP

## Setup

```bash
npm install
```

## Run (stdio)

```bash
npm run dev
```

For production-style usage:

```bash
npm run build
npm run start
```

## VS Code Chat Quickstart (MCP client)

This server uses stdio, so your chat client must launch it as a subprocess.

1. Make sure FlowTime.Sim.Service and FlowTime API are running.
2. Decide the server command you will launch (dev or build).
3. Configure your MCP-enabled chat client to launch the server with env vars.
4. Restart the chat client and verify the tools are available.

Example launch command (dev):

```bash
node --import tsx src/index.ts
```

Example MCP client config (adjust to your client syntax):

```json
{
  "command": "node",
  "args": ["--import", "tsx", "src/index.ts"],
  "cwd": "/workspaces/flowtime-vnext/tools/mcp-server",
  "env": {
    "FLOWTIME_SIM_API_URL": "http://localhost:8090/api/v1",
    "FLOWTIME_SIM_DRAFT_API_URL": "http://localhost:8090/api/v1",
    "FLOWTIME_API_URL": "http://localhost:8080/v1",
    "MCP_MAX_BINS": "1000"
  }
}
```

Suggested chat flow once connected:

- "List available templates."
- "Run template transportation-basic with bins=365 and rng seed 20250327."
- "Get the run summary for run_id."
- "Get the graph for run_id."
- "Get the state window for run_id from bin 0 to 30."
- "Ingest this CSV as series arrivals_baseline."
- "Summarize series arrivals_baseline (basic or expert)."
- "Fit a profile from series arrivals_baseline."
- "Preview the fitted profile."
- "Apply the profile to draft draft-id node arrivals."

Notes:

- If `run_template` is called without `rngSeed`, the MCP server generates one and returns it in the response.
- Draft tools (`create_draft`, `validate_draft`, `generate_model`, `run_draft`) use FlowTime.Sim.Service draft storage endpoints (`/api/v1/drafts/*`).
- `ingest_series`, `summarize_series`, `fit_profile`, and `preview_profile` call Sim data/profile endpoints (`/api/v1/series/*`, `/api/v1/profiles/*`).

## VS Code Task

If you want a one-click server launch in VS Code, use the task in `.vscode/tasks.json`.

1. Run `npm install` in `tools/mcp-server` (once).
2. In VS Code, run `Tasks: Run Task` and select `start-mcp-server`.
3. Adjust the env values in `.vscode/tasks.json` if your services use different URLs.

## Environment Variables

- `FLOWTIME_SIM_API_URL` (default: `http://localhost:8090/api/v1`)
- `FLOWTIME_SIM_DRAFT_API_URL` (default: `FLOWTIME_SIM_API_URL`)
- `FLOWTIME_API_URL` (default: `http://localhost:8080/v1`)
- `MCP_MAX_BINS` (default: `1000`)
- `MCP_REQUEST_TIMEOUT_MS` (default: `30000`)
- `MCP_ORCHESTRATION_TIMEOUT_MS` (default: `120000`)

## Integration Tests (Optional)

Set the following to enable integration tests:

- `MCP_TEST_SIM_API_URL`
- `MCP_TEST_ENGINE_API_URL`
- `MCP_TEST_TEMPLATE_ID` (optional; enables run_template test)
- `MCP_TEST_RNG_SEED` (optional; required for templates with rng blocks)
- `MCP_TEST_DRAFT_SIM_API_URL` (optional; enables draft validation/run tests)
- `MCP_TEST_RUN_ID` (optional; enables get_state_window edge metadata test)
- `MCP_TEST_START_BIN` (optional; used with `MCP_TEST_RUN_ID`)
- `MCP_TEST_END_BIN` (optional; used with `MCP_TEST_RUN_ID`)

Run tests:

```bash
npm test
```
