# MCP Server Implementation Plan (Node/TypeScript)

Status: M-07.01–M-07.04 complete; MCP modeling + analysis delivered via HTTP-only storage-backed drafts/run bundles (see `docs/releases/M-07.02.md`, `docs/releases/M-07.03.md`, `docs/releases/M-07.04.md`, and the tracking docs).

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
- Use Sim draft endpoints backed by the storage abstraction (no direct filesystem access).
- MCP server reads/writes draft content over HTTP and references drafts by `StorageRef`.
- Sim can generate a model bundle from a draft and return a `modelRef` for later runs.

## 7. Phase 2: Data Intake and Profile Fitting

Add tools:
- `ingest_series`, `summarize_series`, `fit_profile`, `map_series_to_inputs`.

Storage:
- Input series are stored via Sim data intake endpoints with provenance captured alongside the series metadata.
- V1 assumes pre-aggregated series; the MCP server only brokers the workflow over HTTP.

Note: profile fitting now lives in FlowTime.Sim so the same fitting logic can be reused across CLI/UI/API in the future.

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

Delivered surfaces for the modeling loop:
- Sim draft endpoints (CRUD/validate/generate/run) backed by storage refs.
- Sim data intake + profile fitting endpoints for pre-aggregated series.
- Template source endpoint for approved templates.
- Run orchestration returning `bundleRef`, with API import reading bundles from storage.

Future work (hardening):
- Auth, quotas, and rate limits for multi-tenant deployments.
- Optional parameter validation and richer run summary surfaces.

## 11. UI Changes

None required for MCP. External chat clients (VS Code, CLI) are sufficient. A future UI integration could be layered on later but is not a dependency.

## 12. Risks and Dependencies

- Template source paths: Sim must see the same draft templates the MCP server writes.
- Run import pathing: engine must be able to read bundle locations.
- Deployment storage: draft templates and data series need a consistent storage location.
- Tool budget tuning: too permissive = high cost; too strict = poor usability.
