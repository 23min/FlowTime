# MCP Modeling Architecture for FlowTime

Status: draft / planning

## 1. Purpose and Scope

This document describes a future-facing architecture for **AI-assisted modeling via chat** using a FlowTime-provided MCP server. It extends the existing MCP "AI analyst" architecture by adding the **authoring + execution + verification loop** needed to build models end to end.

Key goals:
- Enable external chat clients (for example, VS Code) to drive FlowTime modeling through MCP tools.
- Let an AI assistant generate, refine, validate, run, and inspect FlowTime templates.
- Close the loop by executing runs and summarizing results before a user does visual verification.

Non-goals:
- UI design beyond what is required to explain the flow.
- Implementing the MCP server or tools (this is an architecture doc, not code).
- Defining detailed prompt strategies; this stays at the contract/interface level.

---

## 2. Relationship to Existing MCP Docs

This document complements:
- `docs/architecture/ai/mcp-analyst.md`: read-only analyst over the digital twin.
- `docs/architecture/ai/mcp-server-architecture.md`: initial MCP tool surface for graph/state inspection.
- `docs/architecture/ai/mcp-modeling-tools.md`: authoring, run, and promotion tool schemas.
- `docs/architecture/ai/mcp-server-implementation-plan.md`: Node/Python implementation plan and .NET note.

The modeling loop here **adds write-capable tools** (draft template creation, validation, execution) while keeping strong guardrails and explicit promotion workflows.

---

## 3. Server Shape and Deployment Options

Recommended default:
- **One MCP server** that exposes both analyst and modeling tools.
- **Capability profiles** determine which tool sets are enabled (analysis-only vs full modeling).

Optional deployment pattern:
- **Same codebase, two deployments** (read-only analyst vs authoring/run) when stronger isolation is required.

This keeps the UX simple for external chat clients while preserving a clear upgrade path to stricter separation.

---

## 4. Actors and Surfaces

**Actors**
- **User**: Describes system intent and reviews results.
- **AI assistant**: Iteratively authoring and validating models.
- **MCP server**: FlowTime-provided bridge that exposes analyst + modeling tools.

**Surfaces**
- **External chat client**: VS Code, CLI, or other MCP-capable tool.
- **FlowTime.Sim**: Template generation and model authoring surface.
- **FlowTime Engine**: Deterministic execution surface.
- **Artifact store**: Run outputs (CSV/JSON) and diagnostics.
- **Template registry**:
  - `templates/`: approved templates only.
  - **Working area**: drafts and in-progress templates (default `templates-draft/`).

---

## 5. Template Lifecycle and Working Area

Templates are treated as **approved assets** in `templates/`. AI-generated or AI-modified templates live in a **working area** until they pass validation and review.

Recommended lifecycle:
1. **Create or import draft**: Start from scratch or copy an approved template into the working area.
2. **Iterate in working area**: Apply diffs, validate, and run tests.
3. **Promote**: Move into `templates/` only when the user explicitly approves.

The working area is configurable; examples:
- `templates-draft/`
- `data/working-templates/`
- `data/workbench/templates/`

The MCP server should treat `templates/` as read-only unless a promotion action is explicitly requested and confirmed.

---

## 6. Closed-Loop Modeling Flow (Chat -> Run -> Inspect)

```
User (chat client)
  |
  | 1) Describe system + goals
  v
AI assistant
  |
  | 2) Build/update structured model spec
  v
MCP server (authoring tools)
  |
  | 3) Create/patch draft template (working area)
  | 4) Validate (schema + semantic checks)
  v
Execution (Sim and/or Engine)
  |
  | 5) Run model (budgeted)
  v
Artifacts + diagnostics
  |
  | 6) Inspect inputs, results, and warnings
  v
AI assistant
  |
  | 7) Summarize outcomes, propose changes
  v
Repeat until converged
```

The AI should **inspect results before the user**: run summaries, warnings, diagnostics, KPI deltas, and baseline comparisons should be provided in the chat before any manual UI review.
The analyst toolset (graph + series inspection) is part of every loop to verify both inputs (for example, PMFs and arrivals) and outputs.

---

## 7. Minimal Capability Set (Tool Categories)

Tool names are illustrative; the key is the **capability surface**.
Detailed tool schemas live in `docs/architecture/ai/mcp-modeling-tools.md`.

### 7.1 Template Discovery and Draft Management
- List approved templates and metadata.
- Create a draft from scratch or by cloning an approved template.
- Load and diff draft templates.
- Enumerate working drafts with status (last validation, last run).

### 7.2 Authoring and Patch Operations
- Apply structured changes (patch/diff) to draft templates.
- Edit with schema-aware hints (node types, expressions, classes).
- Record a change log for traceability (template version history).

### 7.3 Validation and Diagnostics
- Schema validation against FlowTime template specs.
- Semantic checks (e.g., missing inputs, invalid expressions, class routing gaps).
- Return actionable errors and hints suitable for chat iteration.

### 7.4 Execution and Budgeted Runs
- Generate models from templates (Sim).
- Execute models (Engine or Sim) with explicit run budgets:
  - max bins
  - max runtime
  - max concurrent runs
  - allowed parameter ranges

### 7.5 Inspection and Verification (Analyst in Loop)
- Summaries: KPIs, errors, warnings, runtime stats.
- Artifact access: time series, topology, provenance, and logs.
- Comparison tools: baseline vs draft vs scenario.
- Input verification: inspect PMFs/arrivals and other input series to confirm expected distributions.
- Reuse the analyst tools described in `mcp-analyst.md` for graph and series inspection.

### 7.6 Promotion and Governance
- Promote a draft into `templates/` (explicit user confirmation).
- Record provenance: source template, run IDs, validation status.
- Optional: require clean validation + passing smoke run.

---

## 8. Guardrails and Safety

Guardrails are required because this MCP surface is run-capable:

- **Read-only by default** for `templates/`; drafts are the only writable area.
- **Explicit promotion** with user confirmation for anything entering `templates/`.
- **Run budgets** enforced server-side: time window limits, node limits, run caps.
- **Parameter fences** for any scenario mutations or overrides.
- **Audit trail**: record which changes the AI made, which tools were invoked, and the run IDs generated.
- **Capability profiles**: analysis-only vs full modeling; deploy separately if stricter isolation is required.
- **Strict validation in MCP**: treat engine/info warnings for dependency contracts and edge semantics as **hard errors** during MCP model generation (engine warnings remain info-level for legacy templates).

These guardrails should be enforced in the MCP server, not left to the model.

---

## 9. Execution Strategy (Sim vs Engine)

The MCP server should make it explicit which surface is used:

- **Sim-first** for template authoring and parameter checks.
- **Engine** for deterministic execution and artifact inspection.

Depending on the phase:
- **Draft phase**: Sim-only runs, fast validation.
- **Verification phase**: Engine runs for deterministic artifacts.
- **Regression checks**: compare draft run to baseline or approved template run.

---

## 10. Error Handling and Iteration

When a run fails, the MCP server should return structured diagnostics:
- Parse errors (line/column, offending path).
- Schema violations (required fields, invalid enums).
- Semantic failures (e.g., missing topology edges, invalid class routing).
- Runtime issues (timeouts, empty bins, invalid expressions).

The assistant should surface these as:
- Clear error summaries.
- Suggested fixes.
- Follow-up questions to resolve ambiguity.

---

## 11. Low-Friction Onboarding

The goal is to let users start modeling without deep FlowTime knowledge. Recommended practices:

- **Guided intake**: a short questionnaire captures system intent, arrivals, capacity, SLAs, and KPIs.
- **Starter templates**: pick a base template from a small curated set and ask only for missing parameters.
- **Simple flow spec**: allow a minimal spec (stages, rates, constraints) that the assistant translates into a template draft.
- **Progressive disclosure**: begin with a minimal single-class model, then introduce classes, routers, and PMFs only when needed.
- **Inline help**: quick explanations of node kinds, metrics, and constraints from schemas and docs.
- **Validation-first**: run `validate_draft` early and translate analyzer warnings into plain language.

These patterns reduce cognitive load and make the chat experience feel like guided modeling rather than authoring YAML.

### Data Intake and Profile Fitting

Users often have real data (CSV exports, dashboards, or pasted tables) that can drive model inputs. The assistant should make this easy:

- **Accept data in common forms**: pasted tables or CSV files with timestamps and values.
- **Map columns to intent**: identify which series represents arrivals, capacity, or stage-specific metrics.
- **Infer bins**: detect bin size and seasonality from timestamps.
- **Summarize and confirm**: show min/avg/peak, percentiles, and detected periodicity before using the data.
- **Generate input profiles**: convert the series into input distributions for arrivals or service times.
- **Charts stay client-side**: if the user only has a chart image, extract a series outside the MCP server and ingest it as data.

To avoid jargon, treat PMFs as **input profiles** or **distributions**. The user can provide:
- Raw samples (CSV of inter-arrival times or durations), which the system bins into a profile.
- Summary stats (average, peak, p95, p99) to synthesize a reasonable profile.
- Qualitative choices (steady vs bursty, light vs heavy tail), which map to preset shapes.

The assistant can then translate these into the template parameters needed for FlowTime.

### Flow Narrative -> Graph Checklist

If a user can describe the flow ("first A, then B, then it splits..."), that is enough to build the **graph skeleton**. Use this checklist to translate a narrative into a DAG draft:

- Identify **stages** as nodes (services, queues, sinks, DLQs).
- Capture **ordering** as edges (A -> B -> C).
- Mark **splits** with proportions or class routing hints.
- Mark **merges** where multiple upstream nodes converge.
- Add **terminals** for final success/failure outcomes.
- Record any **loops** (retries/backoffs) so the draft can add retry edges.

After the graph skeleton is created, fill in parameters (arrivals, capacity, PMFs, SLAs) with defaults and iterate.

Example narrative:

"Requests enter at Gateway, go to Auth, then split 80/20 into Search and Checkout. Checkout sends failures to a DLQ. Both Search and Checkout end at Success."

Example graph skeleton:
- Nodes: Gateway, Auth, Search, Checkout, Success, CheckoutDLQ
- Edges: Gateway -> Auth, Auth -> Search (80%), Auth -> Checkout (20%), Checkout -> CheckoutDLQ (terminal), Search -> Success, Checkout -> Success

---

## 12. Open Questions

- What is the canonical "model spec" representation used during chat?
  - YAML template directly, JSON DTO, or a higher-level spec?
- Which runs are authoritative for validation: Sim, Engine, or both?
- What is the minimum default run budget for interactive use?
- How should template promotion be gated (tests, approvals, required metadata)?

---

## 13. Sources Reviewed

- `README.md`
- `docs/ROADMAP.md`
- `docs/modeling.md`
- `docs/architecture/ai/mcp-analyst.md`
- `docs/architecture/ai/mcp-server-architecture.md`
