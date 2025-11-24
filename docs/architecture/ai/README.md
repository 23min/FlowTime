# AI Analyst over FlowTime via MCP

## 1. Motivation and Scope

This document describes a future-facing architecture where an AI assistant (via the Model Context Protocol, MCP) acts as an **SRE/analyst** over a FlowTime model and its telemetry-backed time-travel interface.

The core idea:

- FlowTime represents a **digital twin of service flows** as a DAG plus time-binned state vectors.
- FlowTime exposes **readable, deterministic, and explainable** APIs: `/graph`, `/state`, `/state_window`, `/runs`, etc.
- An MCP server wraps these APIs as **typed tools** that an AI model can call safely.
- The AI uses these tools to interrogate the DAG, inspect time windows, and produce **natural-language analyses** (incidents, ripple effects, KPIs, scenario comparisons).

This is explicitly a **future architecture** (not yet implemented), but it builds directly on existing FlowTime contracts and roadmap items. The goal is to document what is possible, what is realistic, and what constraints are required.

---

## 2. High-Level Concept: AI as an Analyst over the Digital Twin

FlowTime today already looks like something an AI could analyze:

- **Topology**: a DAG of nodes (services, queues, adapters, DLQs, etc.) and edges (flows between them).
- **Time-binned state**: per-node/per-class vectors such as arrivals, served, errors, backlog, latency, capacity, cost.
- **Time-travel interface**: the ability to query a single bin (`/state`) or a window (`/state_window`).
- **Telemetry-backed**: gold contracts for real telemetry (5m/hourly bins), with synthetic data feeding the same shape.

An MCP server can expose these underlying capabilities as **tools** to an AI model, which then becomes a kind of "virtual SRE" that can:

- Traverse upstream/downstream dependencies in the DAG.
- Inspect historical periods before/during/after an incident.
- Compute simple metrics (recovery time, peak backlog, SLA shortfalls) over series.
- Narrate what happened across the system in human language.
- Compare baseline vs scenario runs (where FlowTime provides them).

The rest of this document describes:

- What is clearly possible with this approach.
- Where it becomes brittle or unrealistic if not designed carefully.
- A concrete minimal tool set.
- Safety and performance considerations.

A companion document (`mcp-server-architecture.md`) will go into tool schemas and API mappings.

---

## 3. What Is Clearly Possible with MCP + FlowTime

Given FlowTime's surface today, several capabilities are straightforward and realistic:

### 3.1 DAG Interrogation

Using a tool that wraps `/graph`, an AI can:

- Enumerate nodes, edges, subsystems, and classes.
- Follow upstream/downstream chains from a given node.
- Identify fan-in/fan-out points, shared dependencies, and critical paths.

This is simply graph traversal on structured JSON and is well within MCP's intended use.

### 3.2 Time-Window State Inspection

Using tools that wrap `/state` and `/state_window`, an AI can:

- Retrieve per-node/per-class time-binned series for a chosen window.
- Look at arrivals, served, errors, backlog, latency, capacity, etc.
- Detect spikes, drops, changes in slope, and other simple patterns in the data.

This is no different from current tools that let models operate over JSON arrays.

### 3.3 Incident-Style Questions

When an operator asks, for example:

- "Describe the outage and what happened before and after."
- "What was the recovery time after `serviceX` went down?"
- "Which upstream or downstream services were affected, and for how long?"

The AI can:

1. Call a tool to get the relevant graph slice.
2. Call a time-window tool for the interval of interest.
3. Inspect metrics like backlog, capacity, served, errors, latency.
4. Infer:
   - When a node effectively went down (capacity near zero, errors spike, or served collapses).
   - How quickly backlog accumulated and drained.
   - Which neighbors (upstream/downstream) saw correlated degradation.
5. Produce a natural-language narrative that describes the timeline and impact.

The quality of these answers improves significantly if the MCP server provides small analytic helpers (see below), but even with raw series, basic narratives are achievable.

### 3.4 Simple KPI Computation

FlowTime already defines time-series metrics for:

- Backlog and queue depth.
- Served and capacity.
- Latency and error rates.

Given a definition such as:

- **Recovery time**: the smallest time \( t \ge t_0 \) where a backlog series \( Q[t] \) returns to (or near) a baseline or zero.

The AI can:

- Compute time until backlog returns to zero or baseline.
- Compute time until capacity is restored.
- Compute time until upstream nodes are no longer blocked.
- Compute time until downstream queues drain.

These are all simple operations over arrays and timestamps, which GPT-class models routinely handle when given clear, structured data.

### 3.5 Read-Only "Analyst Interface"

MCP is designed for read-then-reason workflows:

- Tools describe exactly what the model can call.
- By default, tools are read-only unless explicitly designed otherwise.
- This matches the role of an "AI analyst" who is allowed to inspect the twin and compute insights, not modify production systems.

---

## 4. Where Things Get Brittle (Limits and Failure Modes)

The optimistic picture above only holds reliably if we design the MCP layer carefully. Some important caveats:

### 4.1 Raw Series vs Curated Helpers

If you:

- Expose only raw arrays (e.g., `arrivals[]`, `served[]`, `backlog[]`, `capacity[]`) and
- Ask the model to perform all detection and metric computations itself,

then you risk:

- Off-by-one errors in bin indexing.
- Misunderstanding of units (bins vs minutes, rates vs counts).
- Fragile behavior in corner cases (multiple overlapping incidents, noisy data).

A more robust approach is to implement **semantic helpers** in the MCP server:

- `detect_outages(nodeId, start, end, thresholds...)`
- `compute_recovery_time(nodeId, outageStart, baselineDefinition...)`
- `find_affected_neighbors(nodeId, outageWindow)`

These helpers:

- Call FlowTime APIs to fetch the needed series.
- Run small, well-tested algorithms server-side.
- Return structured results (incident windows, severity, recovery timestamps) that the AI can narrate.

The AI still reasons and explains, but it does not re-implement your algorithms ad hoc.

### 4.2 Scale: Graph Size and Window Length

If your DAG is small (dozens of nodes) and windows are modest, you can safely:

- Send a full graph and relevant series to the model.

As the system grows (hundreds or thousands of nodes), you must:

- Filter by:
  - `subsystemId`
  - `classId`
  - node subsets of interest
  - narrower time windows
- Avoid large cartesian products (all nodes × long windows) in a single tool response.

Otherwise you will:

- Hit token limits for the model.
- Make reasoning unnecessarily hard.

The MCP server should therefore:

- Encourage scoped queries via tool parameters.
- Potentially offer pre-filtered views (e.g., "graph slice around node X").

### 4.3 Ambiguous Definitions

Metrics like "recovery time" or "affected duration" must have explicit definitions:

- Is recovery defined as backlog hitting exactly zero or within a threshold of baseline?
- How is baseline computed (previous day, previous week, moving average)?
- How do we treat flapping (partial recoveries, regressions)?

These are semantics questions, not MCP questions. To be reliable, you should:

- Encode definitions in the MCP server's helper tools, or
- Document them precisely and provide examples.

### 4.4 Multiple and Overlapping Incidents

Real time series often contain:

- Multiple outages.
- Gradual degradations instead of sharp steps.

Expecting the model to discover and label all such structure purely from raw arrays is fragile. Instead:

- Provide tools like `detect_outages` that return a **set of detected incidents** with:
  - `startTs`, `endTs`
  - type (e.g., outage, degradation)
  - severity
  - key metrics.

The AI then focuses on explanation and correlation, not low-level pattern mining.

### 4.5 What-If and Scenarios

For what-if questions such as:

> "If the autoscaler were 1-bin faster, would the outage have resolved earlier?"

This is realistic if FlowTime supports scenario runs where:

- You can clone a baseline run.
- Apply controlled parameter changes (e.g., capacity multipliers, retry timing).
- Run the engine and get new time series.

The MCP server can then:

- Expose tools like `get_runs` and `run_scenario`.
- Allow the model to:
  - Compare baseline vs scenario.
  - Compute delta KPIs.

However, these tools must be **constrained**:

- Limit which parameters can change.
- Enforce rate limits and budgets for scenario runs.
- Prefer pre-canned scenario variants in early versions.

---

## 5. Minimal MCP Tool Set (Conceptual)

A practical v1 MCP server for FlowTime could be built around a small set of tools. These tools are described conceptually here; schemas and API mappings live in `mcp-server-architecture.md`.

### 5.1 `get_graph`

- Purpose: Expose the FlowTime DAG structure.
- Inputs:
  - Optional filters: `runId`, `subsystemId`, `classId`.
- Outputs:
  - `nodes: [{ id, name, type, subsystemId, ... }]`
  - `edges: [{ fromNodeId, toNodeId, classId?, ... }]`
  - Optional `subsystems` and `classes` metadata.
- Backed by: FlowTime's `/graph` or equivalent.

### 5.2 `get_state_window`

- Purpose: Get per-node/per-class series over a time window.
- Inputs:
  - `start`, `end` (timestamps or bin indices).
  - Optional filters: `runId`, `nodeIds[]`, `classIds[]`, `metrics[]`.
- Outputs:
  - For each node (and optionally class): aligned arrays for metrics such as:
    - `arrivals[]`, `served[]`, `errors[]`, `backlog[]`, `latency[]`, `capacity[]`, etc.
- Backed by: FlowTime's `/state_window` (or a variant better aligned with MCP).

### 5.3 `detect_outages`

- Purpose: Detect outage/degradation intervals for a node based on its series.
- Inputs:
  - `nodeId`
  - `start`, `end`
  - Detection parameters (thresholds, minimum duration, choice of metric).
- Outputs:
  - `incidents: [{ startTs, endTs, type, severity, metricsSnapshot }]`.
- Implementation:
  - Server-side: uses `get_state_window` internally and applies simple heuristics.

### 5.4 `compute_recovery_time`

- Purpose: Compute recovery time for a specific incident.
- Inputs:
  - `nodeId`
  - `outageStartTs`
  - Definition parameters (e.g., backlog vs baseline threshold, capacity threshold).
- Outputs:
  - `recoveryTs`
  - `durationBins`
  - `durationMinutes` (or bin units)
  - Optional confidence/notes.
- Implementation:
  - Server-side: uses FlowTime state data and baseline definitions.

### 5.5 `get_runs` and `run_scenario` (Optional for v1)

- `get_runs`:
  - Lists available runs/scenarios (baseline and variants).
- `run_scenario`:
  - Accepts constrained scenario parameters.
  - Triggers a new FlowTime run or selects a precomputed variant.
  - Returns a `runId` for subsequent `get_graph`/`get_state_window` calls.

These optional tools enable the AI to compare baseline versus scenario behavior, but they are not strictly necessary for a v1 read-only analyst.

---

## 6. Safety, Performance, and Scope

To keep the MCP integration safe and practical, several constraints are important:

### 6.1 Read-Only in v1

- All tools should be read-only initially.
- No tools that can change production systems or mutate underlying telemetry.
- Scenario execution (if exposed) should be treated as a separate, controlled capability.

### 6.2 Scoped Queries

- Tools should encourage filtered, scoped access:
  - Limit time-window length.
  - Limit number of nodes and classes per call.
  - Encourage subsystem- or node-focused analysis.

This protects both the FlowTime backend and the model's context window.

### 6.3 Clear Metric Semantics

- The MCP server must document and enforce metric semantics:
  - Units (counts vs rates vs durations).
  - Bin sizes and alignment.
  - Definitions of "outage", "recovery", "affected", "baseline".

This reduces ambiguity and makes AI answers more consistent.

### 6.4 Rate Limiting and Guardrails

- Apply rate limits at the MCP server:
  - Limit frequency and concurrency of heavy tools (especially `run_scenario`).
  - Possibly precompute common windows and runs.

---

## 7. Documentation and Future Work

This document is intentionally conceptual and aspirational. A companion document, `mcp-server-architecture.md`, should provide:

- Concrete MCP tool schemas for FlowTime.
- Exact mappings from FlowTime APIs to MCP tools.
- Example tool call sequences for common questions.
- Recommendations for server implementation (language/runtime choices, testing).

Future work may also include:

- Example conversations demonstrating:
  - Incident description.
  - Ripple analysis.
  - Baseline vs scenario comparison.
- Sample code for a small MCP server that wraps a local FlowTime API instance.

The overarching point is that **MCP is a realistic and well-suited mechanism** to let an AI interrogate the FlowTime DAG and time-binned state, acting as a smart, read-only analyst over your digital twin.
