# MCP Server Implementation Plan (Node/TypeScript)

Status: planning

## 1. Goals

- Validate that MCP chat integration is viable with FlowTime quickly.
- Keep the MCP server modular and isolated from core FlowTime code.
- Use FlowTime.Sim.Service and FlowTime API over HTTP for all execution.
- Deliver a minimal closed-loop modeling flow (run -> inspect -> iterate).
- Default PoC language: Node/TypeScript (Python is a viable alternative if heavier local data fitting is needed).

## 2. Non-goals

- Build a UI or embed chat inside FlowTime.UI.
- Ship a production-grade auth story in the first iteration.
- Re-implement FlowTime functionality inside the MCP server.

## 3. Repo Placement and Isolation

Recommended layout for separation:
- `tools/mcp-server/` (or `mcp/`) at repo root.
- Own `package.json` or `pyproject.toml` and lockfile.
- No compile-time dependencies on FlowTime projects.
- Clear boundary: MCP server is a client of FlowTime APIs only.

This keeps development on other features unblocked and lets MCP work evolve independently.

## 4. Transport and Hosting

- **Local dev**: MCP over stdio (console app spawned by VS Code).
- **Deployment**: containerized service (Azure Container Apps or App Service).
- Optional later: HTTP/SSE transport if a client requires it.

## 5. Phase 0: Minimal PoC (Prove the Loop)

### Tools
- `run_template`: orchestrate a run through FlowTime.Sim.Service.
- `get_run_summary`: return warnings and a short KPI summary.
- `get_state_window`: pull series for verification.
- `get_graph`: inspect topology.
- `list_templates`: optional, for discovery.

### Flow
1. User asks to run a template with parameters.
2. MCP server calls Sim orchestration and gets a run bundle.
3. MCP server imports the run into FlowTime API.
4. MCP server fetches summary + series and responds in chat.

### Dependencies (existing endpoints)
- Sim:
  - `POST /api/v1/orchestration/runs`
  - `GET /api/v1/templates` (optional)
  - `POST /api/v1/templates/refresh` (optional)
- Engine:
  - `POST /v1/runs` (import bundle)
  - `GET /v1/runs/{runId}/graph`
  - `GET /v1/runs/{runId}/state_window`
  - `GET /v1/runs/{runId}/metrics` or `GET /v1/runs/{runId}/state`

### Guardrails
- Fixed run budgets (max bins, max runtime, max concurrency).
- Safe defaults for parameters when missing.
- Read-only access to approved templates.

## 6. Phase 1: Draft Authoring

Add tools:
- `create_draft`, `apply_draft_patch`, `validate_draft`, `generate_model`.

Draft storage:
- Default to `templates-draft/`.
- Option A: configure Sim to read from `templates-draft/` during MCP runs.
- Option B: add a Sim endpoint that accepts inline template content or a draft path list.

This phase may require a small FlowTime.Sim.Service extension if per-request template sources are needed.

## 7. Phase 2: Data Intake and Profile Fitting

Add tools:
- `ingest_series`, `summarize_series`, `fit_profile`, `map_series_to_inputs`.

Storage:
- Keep input series under a dedicated MCP data folder (for example `data/mcp/series/`).
- Track provenance for each series (source, timestamp, units).

Note: profile fitting lives in the MCP server for the PoC, but it is a natural fit to move into FlowTime.Sim later for reuse across CLI/UI/API.

## 8. Phase 3: Hardening

- Auth and scoped API keys.
- Rate limits and run quotas.
- Audit logging of tool usage.
- Caching for repeated graph/state queries.
- Clear error mapping for chat responses.

## 9. .NET Implementation Note

A .NET MCP server is viable and aligns with the FlowTime stack, but it will likely require more MCP protocol plumbing due to fewer off-the-shelf libraries. A practical path is:
- Build the PoC in Node/TypeScript for speed (Python remains an alternative if needed).
- Port to .NET once the tool set stabilizes.

## 10. FlowTime Changes: Expected Scope

PoC can be modular and use existing HTTP APIs. Likely changes for a full modeling loop:
- Add Sim endpoints to accept inline draft templates or multiple template roots.
- Add a structured "run summary" endpoint if current surfaces are insufficient.
- Optional: parameter validation endpoint if not already exposed.

## 11. UI Changes

None required for MCP. External chat clients (VS Code, CLI) are sufficient. A future UI integration could be layered on later but is not a dependency.

## 12. Risks and Dependencies

- Template source paths: Sim must see the same draft templates the MCP server writes.
- Run import pathing: engine must be able to read bundle locations.
- Deployment storage: draft templates and data series need a consistent storage location.
- Tool budget tuning: too permissive = high cost; too strict = poor usability.
