---
id: M-023
title: Time Scrubber
status: done
parent: E-17
depends_on:
  - M-022
acs:
  - id: AC-1
    title: '**AC-1: `selectedBin` state.** The What-If page introduces `let selectedBin = $state<number | null>(null)`. `null`
      means "mean mode" (existing behavior, unchanged). A number means "bin T mode".'
    status: met
  - id: AC-2
    title: '**AC-2: Scrubber control.** Below the topology panel (before the warnings panel), a scrubber section renders when
      a model is compiled and has `bins > 1`: - Label: `Bin: {T} / {bins - 1}` (or `Mean` when null). - A range `<input>`
      from `0` to `bins - 1`, step `1`, bound to `selectedBin` (0 when null). - A `Mean` toggle button / checkbox that sets
      `selectedBin` back to `null`. - When `selectedBin` is `null`, the slider displays at position 0 but is visually dimmed
      or labeled "Mean". - `data-testid="bin-scrubber"` on the range input, `data-testid="bin-mean-toggle"` on the reset button.'
    status: met
  - id: AC-3
    title: '**AC-3: Scrubber hidden when `bins === 1`.** Models with a single bin have nothing to scrub. The scrubber section
      is not rendered when `bins <= 1`.'
    status: met
  - id: AC-4
    title: '**AC-4: `buildMetricMap` accepts optional bin.** The signature of `buildMetricMap` in `topology-metrics.ts` is
      extended: ```typescript export function buildMetricMap( graph: EngineGraph, series: Record<string, number[]>, bin?:
      number, // undefined → mean mode ): MetricMap; ``` When `bin` is a valid index, use `values[bin]` instead of the mean.
      When `bin` is out of range or undefined, fall back to the mean. The existing mean behavior is unchanged — no callers
      break.'
    status: met
  - id: AC-5
    title: '**AC-5: `buildEdgeMetricMap` accepts optional bin.** The same optional `bin` parameter is added to `buildEdgeMetricMap`
      (from M-022), with identical semantics.'
    status: met
  - id: AC-6
    title: "**AC-6: `metricMap` and `edgeMetricMap` react to `selectedBin`.** In `+page.svelte`, both derived stores read
      `selectedBin` and pass it to the metric builders. Svelte's reactivity ensures the topology recolors without re-layout
      when the scrubber moves."
    status: met
  - id: AC-7
    title: '**AC-7: `Chart` accepts `crosshairBin`.** The `<Chart>` component (in `chart.svelte`) gains a new optional prop:
      ```typescript interface Props { // ...existing... crosshairBin?: number; // undefined → no crosshair } ``` When set,
      a vertical SVG line is rendered at the x-position corresponding to `crosshairBin`. The line is styled as a light gray
      or muted dashed line that does not interfere with the hover tooltip or series paths.'
    status: met
  - id: AC-8
    title: '**AC-8: `crosshairX` pure helper.** A new export in `chart-geometry.ts`: ```typescript export function crosshairX(bin:
      number, geom: ChartGeometry): number | null; ``` Returns the SVG x-coordinate for `bin`, or `null` if `bin` is out of
      range. Uses `geom.plotLeft`, `geom.plotRight`, and `geom.bins` for the linear interpolation. Fully unit-tested.'
    status: met
  - id: AC-9
    title: '**AC-9: All charts show crosshair at `selectedBin`.** In `+page.svelte`, every `<Chart>` is passed `crosshairBin={selectedBin
      ?? undefined}`. When the scrubber moves, all charts update the crosshair position in lockstep.'
    status: met
  - id: AC-10
    title: '**AC-10: `buildMetricMap` per-bin tests.** New vitest cases in `topology-metrics.test.ts`: - `bin=0` → returns
      value at index 0, not mean. - `bin=N-1` → returns value at last index. - `bin=N` (out of range) → falls back to mean.
      - `bin=undefined` → returns mean (unchanged behavior, existing test keeps passing). - At least 6 new tests.'
    status: met
  - id: AC-11
    title: '**AC-11: `buildEdgeMetricMap` per-bin tests.** Same coverage as AC-10 but for edge metrics. At least 4 new tests.'
    status: met
  - id: AC-12
    title: '**AC-12: `crosshairX` tests.** In `chart-geometry.test.ts`: - Bin 0 → returns `plotLeft`. - Bin `bins-1` → returns
      `plotRight`. - Bin at midpoint → returns midpoint x. - Bin out of range (negative, >= bins) → returns `null`. - At least
      5 tests.'
    status: met
  - id: AC-13
    title: '**AC-13: Chart crosshair render test (Svelte component smoke test).** Using the existing Vitest + jsdom setup
      (matching the chart-geometry tests), verify that when `crosshairBin` is set, a `<line>` element appears in the Chart
      SVG.'
    status: met
  - id: AC-14
    title: "**AC-14: Scrubber visible and functional.** On the `queue-with-wip` model (multiple bins): - Scrubber is present
      with `data-testid=\"bin-scrubber\"`. - Drag scrubber to bin `N/2` → topology node colors change (at least one node's
      fill differs from the mean-mode color). - All chart SVGs contain a `<line>` element (crosshair) after scrubber is moved.
      - Click `data-testid=\"bin-mean-toggle\"` → crosshair disappears from charts, heatmap reverts to mean colors."
    status: met
  - id: AC-15
    title: '**AC-15: Scrubber absent on single-bin model.** Load `capacity-constrained` model (4 bins — scrubber should appear).
      Load a hypothetical 1-bin model or verify by counting bins from the latency badge area. If no suitable model exists,
      verify the condition is covered by unit test only and note the gap.'
    status: met
  - id: AC-16
    title: '**AC-16: No regression.** All previous vitest (173+) and Playwright (19+) tests pass.'
    status: met
---

## Goal

Add a bin-position scrubber below the topology graph. Dragging it switches the heatmap from "mean across all bins" to "value at bin T", and stamps a crosshair on all charts at the same bin. The user can step through time and watch the model "breathe" — seeing exactly how node load and edge flow evolve bin by bin.

## Context

After M-022, both nodes and edges in the topology graph are colored by the **mean** of their series across all bins. The mean is a useful summary, but it collapses time — users cannot see:

- How does queue depth build up in early bins?
- When does the capacity constraint first bite?
- Where does flow peak in a variable-arrival model?

The time scrubber answers these questions by letting the user "scrub" through the time axis just as a video scrubber steps through frames. All heatmap colors and the chart crosshair update immediately — no new eval, no new WebSocket round-trip.

The M-020 spec explicitly noted this as a future enhancement:
> *"Future enhancement: let the user pick a bin slider (scrub through time)."*

## Acceptance criteria

### AC-1 — **AC-1: `selectedBin` state.** The What-If page introduces `let selectedBin = $state<number | null>(null)`. `null` means "mean mode" (existing behavior, unchanged). A number means "bin T mode".

### AC-2 — **AC-2: Scrubber control.** Below the topology panel (before the warnings panel), a scrubber section renders when a model is compiled and has `bins > 1`: - Label: `Bin: {T} / {bins - 1}` (or `Mean` when null). - A range `<input>` from `0` to `bins - 1`, step `1`, bound to `selectedBin` (0 when null). - A `Mean` toggle button / checkbox that sets `selectedBin` back to `null`. - When `selectedBin` is `null`, the slider displays at position 0 but is visually dimmed or labeled "Mean". - `data-testid="bin-scrubber"` on the range input, `data-testid="bin-mean-toggle"` on the reset button.

**AC-2: Scrubber control.** Below the topology panel (before the warnings panel), a scrubber section renders when a model is compiled and has `bins > 1`:
- Label: `Bin: {T} / {bins - 1}` (or `Mean` when null).
- A range `<input>` from `0` to `bins - 1`, step `1`, bound to `selectedBin` (0 when null).
- A `Mean` toggle button / checkbox that sets `selectedBin` back to `null`.
- When `selectedBin` is `null`, the slider displays at position 0 but is visually dimmed or labeled "Mean".
- `data-testid="bin-scrubber"` on the range input, `data-testid="bin-mean-toggle"` on the reset button.

### AC-3 — **AC-3: Scrubber hidden when `bins === 1`.** Models with a single bin have nothing to scrub. The scrubber section is not rendered when `bins <= 1`.

### AC-4 — **AC-4: `buildMetricMap` accepts optional bin.** The signature of `buildMetricMap` in `topology-metrics.ts` is extended: ```typescript export function buildMetricMap( graph: EngineGraph, series: Record<string, number[]>, bin?: number, // undefined → mean mode ): MetricMap; ``` When `bin` is a valid index, use `values[bin]` instead of the mean. When `bin` is out of range or undefined, fall back to the mean. The existing mean behavior is unchanged — no callers break.

**AC-4: `buildMetricMap` accepts optional bin.** The signature of `buildMetricMap` in `topology-metrics.ts` is extended:
```typescript
export function buildMetricMap(
graph: EngineGraph,
series: Record<string, number[]>,
bin?: number,   // undefined → mean mode
): MetricMap;
```
When `bin` is a valid index, use `values[bin]` instead of the mean. When `bin` is out of range or undefined, fall back to the mean. The existing mean behavior is unchanged — no callers break.

### AC-5 — **AC-5: `buildEdgeMetricMap` accepts optional bin.** The same optional `bin` parameter is added to `buildEdgeMetricMap` (from M-022), with identical semantics.

### AC-6 — **AC-6: `metricMap` and `edgeMetricMap` react to `selectedBin`.** In `+page.svelte`, both derived stores read `selectedBin` and pass it to the metric builders. Svelte's reactivity ensures the topology recolors without re-layout when the scrubber moves.

### AC-7 — **AC-7: `Chart` accepts `crosshairBin`.** The `<Chart>` component (in `chart.svelte`) gains a new optional prop: ```typescript interface Props { // ...existing... crosshairBin?: number; // undefined → no crosshair } ``` When set, a vertical SVG line is rendered at the x-position corresponding to `crosshairBin`. The line is styled as a light gray or muted dashed line that does not interfere with the hover tooltip or series paths.

**AC-7: `Chart` accepts `crosshairBin`.** The `<Chart>` component (in `chart.svelte`) gains a new optional prop:
```typescript
interface Props {
// ...existing...
crosshairBin?: number;  // undefined → no crosshair
}
```
When set, a vertical SVG line is rendered at the x-position corresponding to `crosshairBin`. The line is styled as a light gray or muted dashed line that does not interfere with the hover tooltip or series paths.

### AC-8 — **AC-8: `crosshairX` pure helper.** A new export in `chart-geometry.ts`: ```typescript export function crosshairX(bin: number, geom: ChartGeometry): number | null; ``` Returns the SVG x-coordinate for `bin`, or `null` if `bin` is out of range. Uses `geom.plotLeft`, `geom.plotRight`, and `geom.bins` for the linear interpolation. Fully unit-tested.

**AC-8: `crosshairX` pure helper.** A new export in `chart-geometry.ts`:
```typescript
export function crosshairX(bin: number, geom: ChartGeometry): number | null;
```
Returns the SVG x-coordinate for `bin`, or `null` if `bin` is out of range. Uses `geom.plotLeft`, `geom.plotRight`, and `geom.bins` for the linear interpolation. Fully unit-tested.

### AC-9 — **AC-9: All charts show crosshair at `selectedBin`.** In `+page.svelte`, every `<Chart>` is passed `crosshairBin={selectedBin ?? undefined}`. When the scrubber moves, all charts update the crosshair position in lockstep.

### AC-10 — **AC-10: `buildMetricMap` per-bin tests.** New vitest cases in `topology-metrics.test.ts`: - `bin=0` → returns value at index 0, not mean. - `bin=N-1` → returns value at last index. - `bin=N` (out of range) → falls back to mean. - `bin=undefined` → returns mean (unchanged behavior, existing test keeps passing). - At least 6 new tests.

**AC-10: `buildMetricMap` per-bin tests.** New vitest cases in `topology-metrics.test.ts`:
- `bin=0` → returns value at index 0, not mean.
- `bin=N-1` → returns value at last index.
- `bin=N` (out of range) → falls back to mean.
- `bin=undefined` → returns mean (unchanged behavior, existing test keeps passing).
- At least 6 new tests.

### AC-11 — **AC-11: `buildEdgeMetricMap` per-bin tests.** Same coverage as AC-10 but for edge metrics. At least 4 new tests.

### AC-12 — **AC-12: `crosshairX` tests.** In `chart-geometry.test.ts`: - Bin 0 → returns `plotLeft`. - Bin `bins-1` → returns `plotRight`. - Bin at midpoint → returns midpoint x. - Bin out of range (negative, >= bins) → returns `null`. - At least 5 tests.

**AC-12: `crosshairX` tests.** In `chart-geometry.test.ts`:
- Bin 0 → returns `plotLeft`.
- Bin `bins-1` → returns `plotRight`.
- Bin at midpoint → returns midpoint x.
- Bin out of range (negative, >= bins) → returns `null`.
- At least 5 tests.

### AC-13 — **AC-13: Chart crosshair render test (Svelte component smoke test).** Using the existing Vitest + jsdom setup (matching the chart-geometry tests), verify that when `crosshairBin` is set, a `<line>` element appears in the Chart SVG.

### AC-14 — **AC-14: Scrubber visible and functional.** On the `queue-with-wip` model (multiple bins): - Scrubber is present with `data-testid="bin-scrubber"`. - Drag scrubber to bin `N/2` → topology node colors change (at least one node's fill differs from the mean-mode color). - All chart SVGs contain a `<line>` element (crosshair) after scrubber is moved. - Click `data-testid="bin-mean-toggle"` → crosshair disappears from charts, heatmap reverts to mean colors.

**AC-14: Scrubber visible and functional.** On the `queue-with-wip` model (multiple bins):
- Scrubber is present with `data-testid="bin-scrubber"`.
- Drag scrubber to bin `N/2` → topology node colors change (at least one node's fill differs from the mean-mode color).
- All chart SVGs contain a `<line>` element (crosshair) after scrubber is moved.
- Click `data-testid="bin-mean-toggle"` → crosshair disappears from charts, heatmap reverts to mean colors.

### AC-15 — **AC-15: Scrubber absent on single-bin model.** Load `capacity-constrained` model (4 bins — scrubber should appear). Load a hypothetical 1-bin model or verify by counting bins from the latency badge area. If no suitable model exists, verify the condition is covered by unit test only and note the gap.

### AC-16 — **AC-16: No regression.** All previous vitest (173+) and Playwright (19+) tests pass.
## Technical Notes

### `buildMetricMap` extension

Current implementation:

```typescript
export function buildMetricMap(
    graph: EngineGraph,
    series: Record<string, number[]>,
): MetricMap {
    const map = new Map<string, NodeMetric>();
    for (const node of graph.nodes) {
        const values = findNodeSeries(node.id, series);
        if (values === undefined) continue;
        map.set(node.id, { value: seriesMean(values), label: node.id });
    }
    return map;
}
```

Extended:

```typescript
export function buildMetricMap(
    graph: EngineGraph,
    series: Record<string, number[]>,
    bin?: number,
): MetricMap {
    const map = new Map<string, NodeMetric>();
    for (const node of graph.nodes) {
        const values = findNodeSeries(node.id, series);
        if (values === undefined) continue;
        const value = pickValue(values, bin);
        map.set(node.id, { value, label: node.id });
    }
    return map;
}

function pickValue(values: number[], bin?: number): number {
    if (bin !== undefined && bin >= 0 && bin < values.length) {
        return values[bin];
    }
    return seriesMean(values);
}
```

`pickValue` is a pure helper extractable for direct unit testing.

### `crosshairX` implementation

```typescript
export function crosshairX(bin: number, geom: ChartGeometry): number | null {
    if (bin < 0 || bin >= geom.bins) return null;
    if (geom.bins <= 1) return geom.plotLeft;
    return geom.plotLeft + (bin / (geom.bins - 1)) * (geom.plotRight - geom.plotLeft);
}
```

### Chart crosshair SVG

In `chart.svelte`, after the series `<path>` elements:

```svelte
{#if crosshairBin !== undefined && crosshairX(crosshairBin, geom) !== null}
    <line
        x1={crosshairX(crosshairBin, geom)}
        x2={crosshairX(crosshairBin, geom)}
        y1={geom.plotTop}
        y2={geom.plotBottom}
        stroke="hsl(240 5% 64.9%)"
        stroke-width="1"
        stroke-dasharray="4 2"
        pointer-events="none"
        data-testid="crosshair"
    />
{/if}
```

The crosshair is rendered at pointer-events none so it doesn't interfere with hover.

### Scrubber position in page layout

Insert between the topology panel and the warnings panel:

```svelte
<!-- Time scrubber (m-E17-06) — only when bins > 1 -->
{#if bins > 1}
    <div class="rounded-lg border p-4" data-testid="time-scrubber-panel">
        <div class="mb-2 flex items-center justify-between">
            <div class="text-sm font-semibold">Time</div>
            <div class="text-muted-foreground font-mono text-[11px]">
                {selectedBin !== null ? `Bin ${selectedBin}` : 'Mean'}
            </div>
        </div>
        <div class="flex items-center gap-3">
            <input
                type="range"
                min="0"
                max={bins - 1}
                step="1"
                value={selectedBin ?? 0}
                oninput={(e) => selectedBin = parseInt((e.target as HTMLInputElement).value)}
                class="flex-1"
                data-testid="bin-scrubber"
            />
            <button
                class="rounded border px-2 py-1 text-xs {selectedBin === null ? 'bg-blue-50 border-blue-400' : 'hover:bg-gray-50'}"
                onclick={() => selectedBin = null}
                data-testid="bin-mean-toggle"
            >Mean</button>
        </div>
    </div>
{/if}
```

The `bins` value comes from the `CompileResult.bins` field — store it as `let bins = $state<number>(0)` and populate in `compileModel`.

### Derived store updates

```typescript
const metricMap = $derived.by(() => {
    if (!engineGraph) return new Map();
    const bin = selectedBin !== null ? selectedBin : undefined;
    return normalizeMetricMap(buildMetricMap(engineGraph, series, bin));
});

const edgeMetricMap = $derived.by(() => {
    if (!engineGraph) return new Map();
    const bin = selectedBin !== null ? selectedBin : undefined;
    return normalizeMetricMap(buildEdgeMetricMap(engineGraph, series, bin));
});
```

No layout change — only metrics change. The `$effect` that applies `.has-warning` classes already runs after metric/graph updates, so warning badges remain correct at all scrubber positions.

## Out of Scope

- Play/pause animation (auto-advancing the scrubber through bins) — future polish.
- Highlighting the selected bin in the chart axes/ticks.
- Per-bin warning filtering (show warnings only in the selected bin).
- Synchronized scrubber across multiple chart panels.
- Keyboard navigation of the scrubber (native range input handles this).

## Success Indicator

Load `queue-with-wip` model. Drag the bin scrubber from bin 0 to bin 3. The topology nodes and edges shift colors as the queue builds up across time bins. All charts show a vertical crosshair at bin 3. Click "Mean" — crosshairs vanish, heatmap reverts to mean colors. The whole interaction is seamless: no eval, no network, just derived state recomputing.

## Key References

- `ui/src/lib/api/topology-metrics.ts` — `buildMetricMap`, `buildEdgeMetricMap` (to extend)
- `ui/src/lib/components/chart-geometry.ts` — add `crosshairX`
- `ui/src/lib/components/chart.svelte` — add `crosshairBin` prop
- `ui/src/routes/what-if/+page.svelte` — add `selectedBin` state, scrubber UI, wire `crosshairBin`
- `work/epics/E-17-interactive-what-if-mode/m-E17-05-edge-heatmap.md` — prior milestone
