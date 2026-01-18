# FlowTime MCP Modeling Tools

Status: draft / planning

## 1. Purpose and Scope

This document defines the MCP tool surface for AI-assisted modeling: draft template creation, validation, model generation, run execution, inspection, and promotion.

Companion docs:
- `mcp-modeling.md`: workflow and lifecycle.
- `mcp-analyst.md`: read-only analyst tool surface.
- `mcp-server-architecture.md`: analyst tool schemas and API mapping.

These tools are run-capable and must enforce guardrails.

---

## 2. Capability Profiles

Recommended default:
- **Analyst** profile: read-only tools from `mcp-analyst.md`.
- **Modeling** profile: analyst tools plus the authoring/run tools in this doc.

Optional deployment pattern:
- Same codebase, two deployments (analysis-only vs full modeling) when stronger isolation is required.

The MCP server should expose which profile is active and refuse calls that are out of scope.

---

## 3. Working Area and Identifiers

- Approved templates live under `templates/` and are read-only to MCP tools.
- Drafts live under `templates-draft/` by default; the MCP server can override the path via configuration.
- Draft tools should use `draftId` rather than `templateId` to avoid accidental promotion.
- Read responses should include `etag` or `contentHash`; patch operations should require it to avoid overwriting concurrent edits.

---

## 4. Tool Inventory (Conceptual Schemas)

Tool names are illustrative; the key is the capability surface.

### 4.1 Template Discovery (Approved)
- `list_templates`: list approved templates and metadata (id, title, version, tags, parameter schema hash).
- `get_template`: retrieve approved template YAML and metadata.
- `get_template_schema`: return `template.schema.json` plus the template's parameter schema and defaults.

### 4.2 Draft Management (Working Area)
- `list_drafts`: list draft templates and their status (last validation/run).
- `create_draft`: create an empty draft or clone from an approved template.
- `get_draft`: retrieve draft YAML and metadata.
- `apply_draft_patch`: apply a structured patch (JSON Patch or explicit operations) to draft content.
- `diff_draft`: compare a draft to its base template or previous version.
- `delete_draft`: remove a draft (optional; requires explicit confirmation).

### 4.3 Validation and Diagnostics
- `validate_draft`: schema validation plus invariant/analyzer checks; returns errors and warnings.
- `validate_parameters`: validate a parameter payload against the template parameter schema.

### 4.4 Model Generation and Runs
- `generate_model`: expand a draft into an engine-ready model (returns model artifact ID or path).
- `run_template`: orchestrate a run from a draft template with explicit budgets.
- `run_model`: execute a model or bundle with the Engine (returns runId).
- `get_run_summary`: return warnings, KPI summaries, and run metadata.

### 4.5 Inspection and Verification (Analyst in Loop)
- Use `get_graph`, `get_state_window`, and related tools from `mcp-analyst.md`.
- The modeling workflow should reuse these tools to verify inputs (PMFs, arrivals) and outputs.

### 4.6 Promotion and Governance
- `promote_draft`: move a draft into `templates/` after explicit user confirmation.
- `refresh_templates`: clear template caches in Sim/API so new content is visible.

### 4.7 Data Intake and Profile Fitting
- `ingest_series`: accept pasted tables or CSV uploads and store as a named series.
- `summarize_series`: return min/avg/peak, percentiles, and detected periodicity.
- `map_series_to_inputs`: attach a series to a draft input (arrivals, capacity, stage-specific metrics).
- `fit_profile`: convert samples or summary stats into an input profile (PMF or equivalent).
- `preview_profile`: show the derived profile shape and bins before applying it.
- Chart digitization is out of scope for the MCP server; convert chart images to series client-side, then use `ingest_series`.
Note: profile fitting lives in the MCP server for the PoC, but it is a natural fit to move into FlowTime.Sim later for reuse across CLI/UI/API.

---

## 5. Guardrails and Constraints

Because these tools can execute runs and modify drafts, enforce:
- **Read-only templates**: no mutation of `templates/` except via `promote_draft`.
- **Budgets**: max bins, max runtime, max nodes/series, max concurrent runs.
- **Parameter fences**: enforce allowed ranges and deny unsafe overrides.
- **Rate limits**: throttle heavy operations (generate/run/promote).
- **Audit trail**: record tool calls, draft IDs, run IDs, and promotion events.

Guardrails must be enforced by the MCP server, not the model.

---

## 6. Mapping to Existing FlowTime Surfaces

These tools can be backed by existing CLI/API surfaces:

- Template listing and schema: FlowTime.Sim template endpoints or CLI (`list templates`, `show template`).
- Validation and analyzers: Sim CLI `validate template` and `TemplateInvariantAnalyzer`.
- Model generation: Sim CLI `generate` (or equivalent Sim service endpoint).
- Orchestrated runs: FlowTime-Sim `POST /api/v1/orchestration/runs`.
- Engine runs: `POST /v1/runs` with a bundle path or archive.
- Template refresh: `POST /api/v1/templates/refresh` and `POST /v1/templates/refresh`.
- Run inspection: Engine `GET /v1/runs/{runId}/state_window`, `/graph`, `/metrics`, and artifact endpoints.

The MCP server should translate between tool inputs and these surfaces while keeping tool schemas stable.

---

## 7. Tool Routing Guide (Intent -> Tools)

This guide shows how the assistant should route common user intents to tools. It assumes the client tracks a small session state (`draftId`, `runId`, `baseTemplateId`).

**Start modeling**
- "Start a model for X" -> `create_draft` (pick a base template)
- "Use template Y" -> `create_draft` (clone from approved template)

**Change the draft**
- "Set arrivals to 200/hour" -> `apply_draft_patch`
- "Add a retry policy" -> `apply_draft_patch`

**Validate**
- "Check it" / "validate" -> `validate_draft`
- "Are these params valid?" -> `validate_parameters`

**Run and inspect**
- "Run it" / "simulate" -> `run_template` -> `get_run_summary`
- "Show the series for X" -> `get_state_window` (analyst tool)
- "Show the topology" -> `get_graph` (analyst tool)

**Compare**
- "Compare to baseline" -> `get_run_summary` or `get_state_window` for two runs

**Finalize**
- "Publish this template" -> `promote_draft` (explicit confirmation) -> `refresh_templates`

If required state is missing (no `draftId` or no `runId`), the assistant asks a short clarifying question rather than guessing.
