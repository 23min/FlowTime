# FlowTime MCP Server Architecture

This document drills into the concrete shape of an MCP server that exposes FlowTime's DAG and time-binned state as tools to an AI assistant.

It is a companion to `README.md` in this folder, which motivates the overall "AI analyst" concept.

---

## 1. Goals and Non-Goals

### Goals

- Provide a **small, well-defined set of MCP tools** that wrap FlowTime APIs.
- Let an AI assistant:
  - Navigate the FlowTime DAG (nodes, edges, subsystems, classes).
  - Inspect time windows of state (per-node/per-class series).
  - Call small semantic helpers (outage detection, recovery computation) implemented server-side.
- Keep the tools **read-only** and **scoped** in early versions.

### Non-Goals (for v1)

- Arbitrary scenario creation with unbounded parameter surfaces.
- Direct modification of production systems.
- Full automatic discovery of all incidents and patterns without any server-side heuristics.

---

## 2. Tool Inventory (Conceptual Schemas)

The following tools are proposed as an initial MCP surface. Schemas are described informally here; a concrete implementation would define them using MCP's tool schema syntax.

### 2.1 `get_graph`

**Purpose**: Expose a FlowTime DAG snapshot that an AI can traverse.

**Inputs** (all optional):

- `runId: string` — which run/scenario's graph to use (default: baseline or latest).
- `subsystemId: string` — restrict to a particular subsystem.
- `classId: string` — restrict to flows of a specific class, if supported.

**Outputs**:

- `nodes: Node[]`
- `edges: Edge[]`
- `subsystems?: Subsystem[]`
- `classes?: ClassSummary[]`

Where:

- `Node` includes fields like:
  - `id: string`
  - `name: string`
  - `type: string` (service, queue, adapter, dlq, etc.)
  - `subsystemId?: string`
- `Edge` includes fields like:
  - `fromNodeId: string`
  - `toNodeId: string`
  - `classId?: string`
- `Subsystem` and `ClassSummary` mirror existing FlowTime model concepts.

**Backed by**: FlowTime's `/graph` (or a future, MCP-oriented graph endpoint).

---

### 2.2 `get_state_window`

**Purpose**: Retrieve per-node/per-class time-binned series for a given window.

**Inputs**:

- `start: string` — start timestamp or bin index.
- `end: string` — end timestamp or bin index.
- Optional filters:
  - `runId: string`
  - `nodeIds: string[]`
  - `classIds: string[]`
  - `metrics: string[]` — subset of metrics to return (e.g., `['backlog', 'capacity', 'served']`).

**Outputs**:

- `window: { start: string; end: string; binSize: string; }`
- `series: NodeSeries[]`

Where `NodeSeries` might look like:

- `nodeId: string`
- `classId?: string`
- `metrics: { [metricName: string]: number[] }`

Each array in `metrics` is aligned with the bins defined by `window`.

**Backed by**: FlowTime's `/state_window` (or a close variant).

---

### 2.3 `detect_outages`

**Purpose**: Server-side helper to detect outage/degradation windows for a node.

**Inputs**:

- `nodeId: string`
- `start: string`
- `end: string`
- Optional configuration:
  - `metric: 'capacity' | 'served' | 'backlog' | 'latency'`
  - `threshold: number` — e.g., capacity near zero, latency above X.
  - `minDurationBins: number` — ignore very short blips.

**Outputs**:

- `incidents: Incident[]`

Where `Incident` might be:

- `startTs: string`
- `endTs: string`
- `type: 'outage' | 'degradation'`
- `severity: 'low' | 'medium' | 'high'`
- `metric: string`
- `details?: { [k: string]: unknown }`

**Implementation**:

- Uses `get_state_window` internally.
- Applies simple heuristics (e.g., capacity below threshold for consecutive bins).
- Returns a compact list of incidents for the AI to describe and correlate.

---

### 2.4 `compute_recovery_time`

**Purpose**: Compute recovery time for a specific node and outage.

**Inputs**:

- `nodeId: string`
- `outageStartTs: string`
- `lookaheadEndTs: string` — upper bound for search.
- Optional configuration:
  - `basedOn: 'backlog' | 'capacity' | 'latency'`
  - `recoveryDefinition: { type: 'zero' | 'baseline'; tolerance?: number; baselineWindow?: { start: string; end: string } }`

**Outputs**:

- `recoveryTs: string | null`
- `durationBins: number | null`
- `durationMinutes?: number | null` (or appropriate time unit)
- `notes?: string`

**Implementation**:

- Uses FlowTime state to compute:
  - First bin where backlog returns to near-zero or baseline.
  - Or capacity/latency return to acceptable levels.
- Encodes your chosen definition of "recovery".

---

### 2.5 `get_runs` and `run_scenario` (Optional)

These tools are optional and support what-if analysis.

#### `get_runs`

**Purpose**: Enumerate existing FlowTime runs and scenarios.

**Inputs**: None (or optional filters).

**Outputs**:

- `runs: { id: string; label: string; createdAt: string; kind: 'baseline' | 'scenario'; metadata?: { [k: string]: unknown } }[]`

#### `run_scenario`

**Purpose**: Trigger a new scenario run with constrained parameters.

**Inputs** (example sketch):

- `baseRunId: string`
- `label: string`
- `parameters: { capacityMultiplier?: number; retryDelayMultiplier?: number; outageInjection?: { nodeId: string; start: string; durationBins: number } }`

**Outputs**:

- `runId: string`
- Possibly a short status or ETA if runs are asynchronous.

**Implementation considerations**:

- Strictly controlled parameter surface.
- Strong rate limiting and budgets.
- May initially be disabled or limited to precomputed scenarios.

---

## 3. API → MCP Mapping

This section sketches how FlowTime's existing or planned APIs map onto the MCP tools above.

| FlowTime API          | MCP Tool            | Notes                                      |
|-----------------------|--------------------|--------------------------------------------|
| `/graph`              | `get_graph`        | Possibly extended with filters.            |
| `/state_window`       | `get_state_window` | Direct mapping; may need filter support.   |
| `/state`              | *(not required)*   | Can be covered by a 1-bin window.          |
| `/runs` (planned)     | `get_runs`         | Enumerate baseline + scenarios.            |
| `/run` or `/scenario` | `run_scenario`     | For controlled what-if, if enabled.        |

The MCP server should aim to:

- Keep its tool schemas stable, even if underlying APIs evolve.
- Encapsulate any translation logic (e.g., timestamps → bin indices, unit conversions).

---

## 4. Server Implementation Considerations

### 4.1 Language and Runtime

The MCP server itself can be implemented in any language that can:

- Serve MCP-compatible JSON over stdin/stdout or a socket.
- Call FlowTime APIs over HTTP.

Reasonable options:

- **TypeScript/Node** — convenient for rapid prototyping and MCP examples.
- **Python** — easy for data shaping and experimentation.
- **C#** — aligns with FlowTime; good for long-term maintainability in a .NET-centric stack.

The choice does not affect FlowTime contracts; it is an integration detail.

### 4.2 Testing and Validation

For each tool, the server should have:

- Unit tests that:
  - Validate schema conformance.
  - Cover detection heuristics (e.g., outages, recovery logic).
- Integration tests that:
  - Run against a test FlowTime instance or fixtures.
  - Ensure correct behavior on realistic graphs and time series.

---

## 5. Safety, Performance, and Guardrails

### 5.1 Read-Only Default

- All tools described here are read-only.
- No tools should modify FlowTime models, telemetry, or production systems.
- Scenario execution (if present) should be:
  - Isolated from production.
  - Rate-limited and audited.

### 5.2 Scope Control

- Enforce limits in the MCP server:
  - Maximum time-window length per `get_state_window`.
  - Maximum number of nodes/classes in a single response.
  - Maximum number of incidents returned per `detect_outages`.
- Encourage the AI (via tool descriptions) to:
  - Narrow scope by subsystem, node, or time slice.

### 5.3 Clear Semantics

- Tool descriptions must document:
  - Metric units and binning.
  - Definitions of "outage", "recovery", "affected", and baseline.
  - Any assumptions made by detection heuristics.

This reduces ambiguity and makes AI-generated explanations more predictable.

---

## 6. Example AI Workflows

This section outlines a few representative workflows the AI might perform using these tools. These are not exhaustive, but they demonstrate how the tools compose.

### 6.1 Describe an Outage and Its Impact

1. User: "Describe what happened between 10:00 and 12:00 around `Billing.Settle`."
2. AI:
   - Calls `get_graph` to locate `Billing.Settle` and its neighbors.
   - Calls `detect_outages` for `Billing.Settle` in the requested window.
   - Calls `get_state_window` for `Billing.Settle` and its neighbors over `[10:00, 12:00]`.
3. AI analyzes outputs and responds with a narrative:
   - When capacity dropped.
   - How backlog grew and drained.
   - Which upstream/downstream services were affected.
   - Recovery times and any SLA implications.

### 6.2 Compute Recovery Time After a Node Outage

1. User: "What was the recovery time after `serviceX` went down at 09:35?"
2. AI:
   - Calls `compute_recovery_time` with `nodeId='serviceX'`, `outageStartTs='09:35'`, and appropriate recovery definition.
3. AI explains:
   - The duration until backlog returned to baseline.
   - Any caveats (e.g., flapping, partial recoveries).

### 6.3 Analyze Ripple Effects

1. User: "Which services were affected downstream of `Auth.Validate` during the outage at 15:00, and for how long?"
2. AI:
   - Calls `get_graph` and traces downstream nodes from `Auth.Validate`.
   - Calls `detect_outages` or `get_state_window` for those nodes in a window around 15:00.
3. AI responds:
   - Which nodes saw degraded capacity, backlog spikes, or latency inflation.
   - Approximate propagation delays and durations.

### 6.4 Compare Baseline and Scenario (Optional)

1. User: "Compare baseline with scenario `AutoScaleFast` and summarize the impact on backlog during last week's incident."
2. AI:
   - Calls `get_runs` to find IDs for baseline and `AutoScaleFast`.
   - Calls `get_state_window` for the incident window on both runs.
   - Computes and explains differences in backlog peaks and recovery times.

---

## 7. Summary

A FlowTime MCP server is a realistic and high-value future architecture that:

- Wraps existing FlowTime graph and state APIs as small, typed tools.
- Gives an AI assistant structured, read-only access to the digital twin.
- Lets the AI behave like a smart SRE/analyst, narrating incidents and computing KPIs.

The key to robustness is:

- Implementing semantic helpers (e.g., outage detection, recovery computation) server-side.
- Keeping tools scoped, well-documented, and read-only.
- Aligning metric semantics and constraints with FlowTime's existing contracts.

This document should serve as a reference for any future implementation of a FlowTime MCP server and for evaluating how such an integration fits into the broader FlowTime roadmap.
