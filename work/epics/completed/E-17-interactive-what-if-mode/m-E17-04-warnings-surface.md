# Milestone: Warnings Surface

**ID:** m-E17-04
**Epic:** E-17 Interactive What-If Mode
**Status:** complete
**Branch:** `milestone/m-E17-04-warnings-surface` (off `main`)
**Depends on:** m-E17-03 (Live topology + charts)

## Goal

The What-If page surfaces engine warnings — conservation violations, queue mismatches, non-stationary arrivals, negative values — as banners and node badges. When the user tweaks a parameter to push the system past a limit, warnings light up in real time; when they pull it back, warnings clear. The interactive experience becomes a complete feedback loop: *see not just what changes, but what's wrong*.

## Context

After m-E17-03 the What-If page shows topology, charts, and a latency badge — but nothing tells the user when their model is broken or degenerate. The Rust engine already computes a rich set of warnings during `eval_model` (via `analyze()` in `analysis.rs`), but none of them make it through the session protocol. The `CompileResult` and `EvalResult` messages carry series and timing but drop warnings on the floor.

### Warnings the engine currently produces

From `engine/core/src/analysis.rs`:

| Code | Severity | Fires when | Node context |
|---|---|---|---|
| `arrivals_negative` | warning | `semantics.arrivals` contains `< -1e-6` in any bin | topology node |
| `served_negative` | warning | `semantics.served` contains negative values | topology node |
| `queue_negative` | warning | `queueDepth` column contains negative values | topology node |
| `errors_negative` | warning | `errors` column contains negative values | topology node |
| `served_exceeds_arrivals` | warning | `served > arrivals` in any bin (non-queue kinds only) | topology node |
| `served_exceeds_capacity` | warning | `served > capacity` in any bin | topology node |
| `queue_depth_mismatch` | warning | computed queue vs actual queue differs beyond tolerance | topology node |
| `non_stationary` | warning | first-half vs second-half arrivals mean differs > 25% | topology node |

Each warning has a `node_id`, `code`, `message`, `bins` (the affected bin indices), and `severity`.

**Key property:** because warnings are computed in the post-eval pipeline (which runs on every `eval` call), they are already recomputed per parameter override. The only thing missing is transport + rendering.

## Acceptance Criteria

1. **AC-1: Warnings in protocol — compile response.** `CompileResult` (Rust `protocol.rs`, TS `engine-session.ts`) includes a `warnings: WarningInfo[]` field. The session handler populates it from the initial eval's `result.warnings`. Each entry carries `node_id`, `code`, `message`, `bins`, `severity`.

2. **AC-2: Warnings in protocol — eval response.** `EvalResultMsg` includes the same `warnings: WarningInfo[]` field, populated from the eval's `result.warnings`. The field is **always present** — empty array means "no warnings," not "unknown".

3. **AC-3: Example model that triggers a warning.** A new example model `capacity-constrained.yaml` is added to `ui/src/lib/api/example-models.ts`:
   - `arrivals` const (default 15, tweakable)
   - `capacity` const (default 10, tweakable)
   - `served` expression forcing served equal to arrivals
   - Topology node with `semantics.capacity = capacity`
   - With defaults: `served=15 > capacity=10` → `served_exceeds_capacity` warning fires on bins 0..N
   - Dropping `arrivals` below `capacity` clears the warning
   - Raising `capacity` above `arrivals` clears the warning

4. **AC-4: Warnings banner.** When the warnings array is non-empty, the What-If page shows a colored alert banner near the top (below the model picker). The banner shows:
   - Warning count
   - Compact list of `{node_id} · {code}` entries (up to 5, then "…and N more")
   - A severity color (amber for `warning`, red for future `error`)
   When the array is empty, the banner is hidden entirely (no empty state).

5. **AC-5: Warning details panel.** Below the topology panel (or as a collapsible section within it), a `Warnings` list shows every warning:
   - Each row: severity icon, `node_id`, `code`, full `message`, affected bin count
   - Rows are grouped by `node_id`
   - The panel is hidden when there are no warnings

6. **AC-6: Node badge on topology graph.** Each topology node with at least one warning is visually flagged:
   - A small ⚠ icon overlay in the top-right of the node, OR
   - A red/amber outline around the node
   - Implementation: Svelte-level SVG overlay on top of the dag-map-view SVG, using node positions exposed by dag-map-view (or computed from the DOM)
   - When the node has no warnings, no badge is rendered
   - Badges update reactively as the warnings array changes

7. **AC-7: Warnings update reactively on eval.** When a parameter change clears a warning, the banner, panel, and node badge all disappear. When a change introduces a warning, they all appear. No page reload.

8. **AC-8: Pure unit tests.**
   - `format.ts` or new `warnings.ts`: group-by-node helper, severity-to-class mapping (8+ tests)
   - Tests use fixtures with 0, 1, N warnings across multiple nodes
   - Unknown warning codes fall back to a default icon/color

9. **AC-9: Playwright E2E.** New spec cases:
   - Load `capacity-constrained` model → warnings banner visible, panel has ≥1 entry, `Service` node shows the badge
   - Tweak `capacity` from 10 to 20 → banner disappears within 500ms, panel empty, badge gone
   - Tweak `capacity` back to 10 → banner returns
   - Load `simple-pipeline` → **no** banner, **no** panel (no topology warnings possible)

10. **AC-10: Compile error surface unchanged.** Compile errors (invalid YAML, missing grid) still flow through the existing `error` field on the Response envelope — not the `warnings` array. Warnings are a separate, orthogonal channel. A model with compile errors produces an error response with no warnings; a valid model produces a result response with a (possibly empty) warnings array.

## Technical Notes

### Rust protocol extension

Add to `engine/cli/src/protocol.rs`:

```rust
#[derive(Debug, Serialize)]
pub struct WarningMsg {
    pub node_id: String,
    pub code: String,
    pub message: String,
    pub bins: Vec<usize>,
    pub severity: String,
}

pub struct CompileResult {
    // ...existing fields...
    pub warnings: Vec<WarningMsg>,
}

pub struct EvalResultMsg {
    // ...existing fields...
    pub warnings: Vec<WarningMsg>,
}
```

In `engine/cli/src/session.rs` `handle_compile` and `handle_eval`, convert `result.warnings` (type `Vec<Warning>` from `analysis.rs`) to `Vec<WarningMsg>` before populating the message.

### TypeScript type

Add to `ui/src/lib/api/engine-session.ts`:

```typescript
export interface WarningInfo {
    node_id: string;
    code: string;
    message: string;
    bins: number[];
    severity: string;
}

export interface CompileResult {
    // ...existing...
    warnings: WarningInfo[];
}

export interface EvalResult {
    // ...existing...
    warnings: WarningInfo[];
}
```

Existing vitest tests that mock compile/eval responses need `warnings: []` added to their fixtures.

### Warnings helper module

New file `ui/src/lib/api/warnings.ts`:

```typescript
export interface WarningGroup {
    nodeId: string;
    warnings: WarningInfo[];
}

/** Group warnings by node_id, preserving insertion order within each group. */
export function groupWarningsByNode(warnings: WarningInfo[]): WarningGroup[];

/** True if any warning in the array references the given node. */
export function nodeHasWarning(warnings: WarningInfo[], nodeId: string): boolean;

/** Tailwind/CSS class suffix for a severity. Defaults to 'warning'. */
export function severityClass(severity: string): 'warning' | 'error';
```

Pure functions, fully unit-tested.

### Svelte UI changes

- **Banner**: new inline block between model picker and topology panel. Uses existing `AlertCircleIcon` and color tokens. Conditionally rendered on `warnings.length > 0`.
- **Panel**: new section below topology panel (or embedded in it). Iterates `groupWarningsByNode(warnings)`.
- **Node badges**: this is the trickiest part. `dag-map-view` renders an SVG. The Svelte page has the graph container; I can overlay a second SVG positioned absolutely on top with badge circles at the same coordinates as the nodes. Node coordinates come from the dag-map layout — which dag-map-view doesn't currently expose.

  **Pragmatic approach**: query the rendered SVG after each metric update, find nodes by their dag-map-generated `data-node-id` or text label, read their bounding boxes, and render badges at those positions. Done via `bind:this` + `tick()` + `getBoundingClientRect`. Not ideal but avoids modifying `dag-map-view`.

  **Cleaner approach** (optional, if the pragmatic one is messy): extend `dag-map-view` to accept a `nodeBadges: Map<string, { icon, color }>` prop and render badges as part of its SVG. This is a cross-cutting change to the shared component.

  I'll start with the pragmatic approach and fall back to extending the component only if positioning is unreliable.

### Capacity-constrained example model

Add to `ui/src/lib/api/example-models.ts`:

```typescript
const CAPACITY_CONSTRAINED: ExampleModel = {
    id: 'capacity-constrained',
    name: 'Capacity constrained',
    description: 'Served forced equal to arrivals; exceeds capacity by default. Drop arrivals or raise capacity to clear the warning.',
    yaml: `grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [15, 15, 15, 15]
  - id: capacity
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "arrivals"
topology:
  nodes:
    - id: Service
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        capacity: capacity
  edges: []
  constraints: []
`,
};
```

**Important:** verify this model actually produces the `served_exceeds_capacity` warning during Rust unit testing. Adjust values if needed.

### Field naming

The Rust `Warning` struct uses `node_id` (snake_case). MessagePack serialization preserves the field name. On the TS side, I'll keep it as `node_id` rather than converting to `nodeId` — matches what's on the wire, matches the C# `RustWarning` DTO (which already uses `NodeId` but is a separate struct).

## Out of Scope

- **Suggestion actions** — no "click to fix" — the user tweaks sliders manually.
- **Animated badge transitions** — plain show/hide is fine.
- **Bin-level warning visualization** — chart doesn't highlight the specific bins where warnings occurred (future polish).
- **Warning history / timeline** — only the current state is shown.
- **Custom warning rules / thresholds** — the engine's defaults stand.
- **Editable severity mapping** — warning/error for now, future may add "info" or "critical".
- **Error surface** — compile/protocol errors stay in their existing path.

## Key References

- `engine/core/src/analysis.rs` — warning generation
- `engine/core/src/compiler.rs` `eval_model` — runs analysis on every eval
- `engine/cli/src/protocol.rs` — `CompileResult`, `EvalResultMsg`
- `engine/cli/src/session.rs` — `handle_compile`, `handle_eval`
- `ui/src/lib/api/engine-session.ts` — client types
- `ui/src/lib/components/dag-map-view.svelte` — existing graph renderer
- `ui/src/routes/what-if/+page.svelte` — target page
- `work/epics/E-17-interactive-what-if-mode/m-E17-03-live-topology-and-charts.md` — prior milestone

## Success Indicator

Load `capacity-constrained` → amber banner at top says "1 warning: Service · served_exceeds_capacity", `Service` node on the topology graph has a ⚠ badge, warnings panel shows the full message "served > capacity in 4 bins". Drag `capacity` slider from 10 up to 20 — banner disappears, badge gone, panel empty, charts still animate. Drop `capacity` back to 5 — warnings reappear. End-to-end under 200ms per change.
