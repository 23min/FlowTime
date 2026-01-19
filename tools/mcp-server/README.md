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
    "FLOWTIME_DATA_DIR": "/workspaces/flowtime-vnext/data",
    "FLOWTIME_TEMPLATES_DIR": "/workspaces/flowtime-vnext/templates",
    "FLOWTIME_TEMPLATES_DRAFT_DIR": "/workspaces/flowtime-vnext/templates-draft",
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

Notes:

- If `run_template` is called without `rngSeed`, the MCP server generates one and returns it in the response.
- Draft tools (`create_draft`, `validate_draft`, `generate_model`, `run_draft`) expect a FlowTime.Sim.Service instance
  that can see `templates-draft/`. Configure the Sim draft templates root (`FLOWTIME_SIM_DRAFT_TEMPLATES_DIR`)
  so a single Sim instance can resolve draftIds.
- `validate_draft`, `generate_model`, and `run_draft` call the Sim draft endpoints (`/api/v1/drafts/*`).

## VS Code Task

If you want a one-click server launch in VS Code, use the task in `.vscode/tasks.json`.

1. Run `npm install` in `tools/mcp-server` (once).
2. In VS Code, run `Tasks: Run Task` and select `start-mcp-server`.
3. Adjust the env values in `.vscode/tasks.json` if your services use different URLs.

## Environment Variables

- `FLOWTIME_SIM_API_URL` (default: `http://localhost:8090/api/v1`)
- `FLOWTIME_SIM_DRAFT_API_URL` (default: `FLOWTIME_SIM_API_URL`)
- `FLOWTIME_API_URL` (default: `http://localhost:8080/v1`)
- `FLOWTIME_DATA_DIR` (default: `../../data`)
- `FLOWTIME_TEMPLATES_DIR` (default: `../../templates`)
- `FLOWTIME_TEMPLATES_DRAFT_DIR` (default: `../../templates-draft`)
- `MCP_MAX_BINS` (default: `1000`)
- `MCP_REQUEST_TIMEOUT_MS` (default: `30000`)
- `MCP_ORCHESTRATION_TIMEOUT_MS` (default: `120000`)

## Integration Tests (Optional)

Set the following to enable integration tests:

- `MCP_TEST_SIM_API_URL`
- `MCP_TEST_ENGINE_API_URL`
- `MCP_TEST_DATA_DIR` (optional; defaults to `data`)
- `MCP_TEST_TEMPLATE_ID` (optional; enables run_template test)
- `MCP_TEST_RNG_SEED` (optional; required for templates with rng blocks)
- `MCP_TEST_DRAFT_SIM_API_URL` (optional; enables draft validation/run tests)
- `MCP_TEST_TEMPLATES_DIR` (optional; defaults to `../../templates` if set in the server config)
- `MCP_TEST_DRAFT_TEMPLATES_DIR` (optional; defaults to `../../templates-draft` if set in the server config)

Run tests:

```bash
npm test
```
