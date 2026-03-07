# MCP Server Best‑Practice Improvements (Gap Analysis + Proposal)

**Status:** Draft  
**Scope:** FlowTime MCP server implementation vs. `docs/architecture/ai/mcp-server-best-practices.md`  
**Sources reviewed:**  
`tools/mcp-server/src/*`, `tools/mcp-server/README.md`, AI architecture docs in `docs/architecture/ai/`, and the "MCP Server Build Guide" reference provided for this analysis.

> This analysis is based on repository code/docs. It does **not** rely on the YouTube talk transcript.

---

## 1) Executive Summary

The current MCP server is a functional **API wrapper** and draft workflow helper, but it does **not** consistently follow the best‑practice guidance in `mcp-server-best-practices.md`. In particular:

- Tools are **operation‑shaped** (API calls) rather than **outcome‑shaped** (agent workflows).
- Responses are **raw JSON dumps**, not summaries optimized for token budgets.
- Tool descriptions are thin and lack examples, reducing predictability.
- Composition is still **agent‑side** in many workflows (multi‑step chains).

These are fixable without breaking the existing server. The proposal below keeps current tools for compatibility but **adds** a small set of outcome‑oriented tools and summary helpers, introduces stricter schema/contract documentation, formalizes error messaging and guardrails, and codifies security/observability/testing expectations.

---

## 2) Current MCP Implementation Snapshot

**Server location:** `tools/mcp-server`  
**Tool list (current):**

- **Read / inspect:** `get_graph`, `get_state_window`, `get_run_summary`  
- **Run templates:** `run_template`, `run_draft`  
- **Drafts:** `list_drafts`, `create_draft`, `get_draft`, `apply_draft_patch`, `diff_draft`, `validate_draft`, `generate_model`  
- **Data / profiles:** `ingest_series`, `summarize_series`, `fit_profile`, `preview_profile`, `map_series_to_inputs`

**What it does well**
- Enforces a max bin window (`MCP_MAX_BINS`) and validates bin ranges.
- Defaults RNG and returns seed (good reproducibility).
- Concurrency protection for draft patching via `expectedHash` (good guardrail).
- Keeps tooling minimal enough for MVP workflows.

**What it currently does (anti‑patterns)**
- Returns raw API payloads (`get_state_window`, `get_graph`) with no summarization.
- Tool names are mostly API names, not “agent story” tools.
- Errors are thrown as generic exceptions; no “error as prompt” structure.
- No explicit tool examples or structured output schemas for most tools.
- No server‑side composition for common narratives (outage, bottleneck, constraint impact).
- No formal security posture documented (transport, auth, scopes).
- Tool docs do not specify pagination/truncation limits or error recovery guidance.

---

## 3) Best‑Practice Principles vs Current State

This section maps best‑practice principles (from `mcp-server-best-practices.md`) to the current implementation.

### 3.1 Outcomes, not operations

**Best practice:** One tool == one agent story, server composes.  
**Current state:** Tools mostly mirror API endpoints (`get_graph`, `get_state_window`, etc.).  
**Gap:** Agent must stitch together multiple calls and interpret raw data.

**Impact:** Fragile chains and inconsistent reasoning when token limits or subtle semantics exist.

---

### 3.2 Design top‑down from workflows

**Best practice:** Start from user workflows; tools should encode them.  
**Current state:** The toolset reflects a bottom‑up API surface.  
**Gap:** No workflow‑level tools for analysis or verification.

---

### 3.3 Flatten arguments

**Best practice:** Top‑level primitive args, enums, strong defaults.  
**Current state:** Mostly flat arguments for run tools, but several tools still use nested configs (`ingest_series.metadata`, profile payloads).  
**Gap:** Some tools are still “mystery meat” for agents; input expectations are not always obvious.

---

### 3.4 Instructions are context

**Best practice:** Docstrings explain what/when/return + examples.  
**Current state:** Short descriptions, no examples, no explicit error hints.  
**Gap:** Agents guess return shapes; errors are not actionable prompts.

---

### 3.5 Respect the token budget

**Best practice:** Summaries by default, raw data on demand.  
**Current state:** `toToolResult` returns full JSON payloads.  
**Gap:** Large token usage and brittle agent reasoning over raw arrays.

---

### 3.6 Tools vs resources vs prompts

**Best practice:** Separate *actions* (tools) from *browseable objects* (resources) and reusable playbooks (prompts).  
**Current state:** We only expose tools; no MCP resources/prompts are defined.  
**Gap:** Agents must fetch or store large context via tool calls rather than controlled resources or prompt templates.

---

### 3.7 Security posture by design

**Best practice:** Define transport/auth and least‑privilege scopes; treat tool calls as untrusted input.  
**Current state:** No explicit auth model in the server; no scope definitions; no per‑tool privilege labeling.  
**Gap:** Risks for remote deployments and lack of clear security documentation.

---

### 3.8 Reliability and operations

**Best practice:** Timeouts, retries, cancellation, and rate limits are explicit; logs to stderr for STDIO.  
**Current state:** No documented timeouts/retries for upstream calls; no backpressure or quotas.  
**Gap:** Tool loops can overwhelm upstreams; poor failure behavior.

---

### 3.9 Testing strategy

**Best practice:** Tool schema tests + scenario (agent contract) tests.  
**Current state:** MCP tests validate basic execution but do not cover summary‑first outputs, error shapes, or workflow correctness.  
**Gap:** High risk of regressions and agent UX drift.

---

## 4) Proposed Mitigations (Non‑Breaking)

### 4.1 Add outcome‑level tools (keep existing tools intact)

Introduce a small set of **workflow tools** that call existing APIs internally:

1) `summarize_run_health(runId, window)`  
   - Returns: top bottlenecks, backlog risk, SLA flags, constraint‑limited nodes.

2) `explain_constraint_impact(runId, nodeId, window)`  
   - Returns: constraint shortfall, binding periods, delta from unconstrained series.

3) `describe_outage(runId, nodeId, window)`  
   - Uses server‑side heuristics for outage intervals and recovery times.

These tools reduce fragile multi‑step chains and produce high‑signal output.

---

### 4.2 Add summary‑first responses

For heavy tools (`get_state_window`, `get_graph`):

- Default to **summary mode** (counts, ranges, maxima, key bins).
- Support `includeRaw: true` or `detailLevel: "full"` to return full payloads.
- Provide response metadata: `seriesCount`, `binCount`, `tokenEstimate`.

---

### 4.3 Document return shapes and add examples

In `tools.ts`, extend tool metadata to include:

- `outputSchema` for **every** tool.
- At least one success example per tool (short, structured).
- At least one error example for validation cases.


---

### 4.4 Improve error messaging

Replace thrown generic errors with structured payloads:

```json
{
  "error": "Constraint series missing for db_main",
  "hint": "Use Option A dependency node or supply constraint series in template.",
  "code": "constraint_missing_series"
}
```

This aligns with “errors are prompts,” not vague failures.

---

### 4.5 Capability profiles enforced in server
Introduce capability profiles so the agent explicitly opts into a surface area:

- **analysis:** outcome tools only (summaries, explainers).
- **modeling:** draft create/patch/validate/run.
- **raw:** `get_state_window`, `get_graph` with `includeRaw: true`.

This makes the “safe default” the summary path and keeps verbose access gated.

---

### 4.6 Add tool/resource/prompt separation (incremental)

Introduce MCP resources for:
- Run lists, run metadata.
- Template catalogs.

Introduce prompts for:
- "Analyze run health"
- "Explain constraint impact"

This reduces token churn and makes common workflows discoverable.

---

### 4.7 Security posture and guardrails

Document and enforce:
- Transport assumptions (STDIO vs HTTP).
- Auth model (local dev vs remote deployment).
- Read vs write tool separation and explicit scope requirements.
- Logging to stderr for STDIO servers.

---

### 4.8 Reliability limits

Add explicit policies for:
- Timeouts per upstream call.
- Bounded retries with jitter.
- Output size limits and pagination.

---

## 5) What We *Should* Have Built (Ideal Best‑Practice Shape)

If we were designing from scratch per `mcp-server-best-practices.md`:

1) **Outcome tools first, raw tools optional**
   - Example: `explain_bottleneck(runId, window)` instead of `get_state_window`.
2) **Strong defaults + flattened args**
   - Small required inputs (runId, window, focusNode).
3) **Server‑side composition**
   - The MCP server runs internal chains, not the agent.
4) **Summary‑first outputs**
   - Clear summaries and small data samples; raw data only on request.
5) **Structured errors**
   - Error payloads that instruct what to do next.

FlowTime can still expose raw tools, but they should be explicitly “verbose.”

---

## 6) Recommended Outcome‑Level Tool Set (Additive)

These tools can be layered on top of the existing APIs without breaking them:

- `summarize_run_window(runId, window, focus?)`
  - Returns: top bottlenecks, constraint‑limited nodes, SLA risk summary.

- `explain_node_behavior(runId, nodeId, window)`
  - Returns: arrivals/served delta, capacity vs effective capacity, constraint impact.

- `explain_constraint_impact(runId, nodeId, window)`
  - Returns: constraint shortfall, constrained bins, constraint provenance.

- `explain_edge_flow(runId, edgeId, window)`
  - Returns: flow volume, retry delta, edge quality notes.

- `compare_windows(runId, windowA, windowB, focus?)`
  - Returns: changes in bottlenecks and flow/queue deltas.

These align with “one tool = one agent story.”

---

## 7) Output Shape Guidance (Token Budget)

For each summary tool, return:

- `headline`: 1–3 sentence summary.
- `facts`: short bullet list of key metrics.
- `evidence`: small, bounded arrays (top N nodes/edges).
- `nextSteps`: optional hints (“inspect node X”, “expand window”).

Raw payloads should require `includeRaw: true`.

---

## 8) Error Design (Errors as Prompts)

Replace generic throws with structured guidance:

```json
{
  "error": "Constraint series missing for db_main",
  "code": "constraint_missing_series",
  "hint": "Supply constraint series in template or use dependency node (Option A)."
}
```

This dramatically improves agent stability and aligns with best practices.

---

## 9) Tests and Coverage Gaps

Add tests that validate the best‑practice behavior:

- Summary tools return bounded responses.
- Raw tools require explicit `includeRaw: true`.
- Error payloads include `code` and `hint`.
- Outcome tools are deterministic for a given run seed.

This is currently missing in the MCP test suite (only basic tool execution exists).

---

## 10) Security & Ops Checklist (Server‑Side)

Minimum checklist aligned to the build guide:

- Explicit transport choice (STDIO vs HTTP).
- Auth strategy documented (local dev vs remote OAuth).
- Read/write tool separation and scope definition.
- Structured logs (tool name, run id, latency).
- Rate limits or quotas for remote deployments.

---

## 11) Tools to Avoid (Anti‑Patterns)

Avoid tools that mirror raw endpoints or dump full payloads without truncation:

- `get_state_window` without `includeRaw: true` gating.
- `get_graph` without summary‑first output.
- `list_*` tools returning full datasets without pagination.

---

## 12) Relationship to Current MCP Docs

These improvements should be cross‑referenced from:

- `docs/architecture/ai/mcp-modeling.md`
- `docs/architecture/ai/mcp-modeling-tools.md`
- `docs/architecture/ai/mcp-server-architecture.md`

They should explicitly state: “The MCP server is responsible for composition and
summary‑level responses; raw data is opt‑in.”

---

## 13) Implementation Phases (Low‑Risk)

**Phase 1 (Docs + API surface)**
- Add summary tools in `tools.ts`.
- Document output shapes and examples.
- Add error codes and hints.

**Phase 2 (Behavior)**
- Implement summary tools using existing APIs.
- Introduce `includeRaw` gating on heavy tools.

**Phase 3 (Quality)**
- Add tests for summary outputs and error structure.
- Add capability profile enforcement.

---

## 14) Summary Recommendation

Keep the current low‑level tools for compatibility, but **add a top‑down
workflow layer** that:

1) Encodes outcomes rather than API calls.
2) Returns summaries by default.
3) Makes errors actionable.

This closes the gap to best practices without a rewrite and sets the MCP server
up for future multi‑client use (UI, agents, external tooling).
