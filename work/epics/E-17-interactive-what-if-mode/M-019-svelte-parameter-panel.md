---
id: M-019
title: Svelte Parameter Panel
status: done
parent: E-17
depends_on:
    - M-018
---

## Goal

A polished Svelte page where the user picks a model, sees an auto-generated parameter panel (sliders + numeric inputs), and watches series values update live as they tweak parameters. This is the interactive what-if experience — the first real UI that exercises the WebSocket bridge at full speed.

## Context

After M-018, the `/engine-test` smoke test page proves the plumbing works: browser → .NET WebSocket proxy → Rust engine session, round-trip in milliseconds. But the smoke test only tweaks one hard-coded parameter and displays series as flat text. It's a plumbing verification, not a user experience.

M-019 builds the real interactive surface:
- A proper page layout under the existing Svelte UI shell (sidebar + topbar)
- Auto-generated controls from the parameter schema returned by `compile`
- Reactive Svelte stores for each series, bound to UI updates
- Debounced eval on slider drag (no flood of requests mid-drag)
- Loading state while eval is in flight
- Error surface for compile/eval failures

The emphasis is **interactive feel**: the user should be able to grab a slider and see numbers move immediately. Chart/topology visualizations are M-020 — this milestone shows series as small-multiple sparklines or numeric tables that update on parameter changes.

## Acceptance Criteria

1. **AC-1: Route `/what-if`.** A new SvelteKit route exists. The page uses the existing app layout (sidebar + topbar) and appears in sidebar navigation.

2. **AC-2: Model picker.** The page shows at least 3 built-in example models the user can select (simple const+expr, queue with WIP limit, class-based decomposition). Selecting a model compiles it via the engine session. Built-in models are bundled as string constants, not loaded from disk or network.

3. **AC-3: Parameter panel auto-generation.** After `compile`, the panel renders one control per parameter:
   - **Scalar parameters** (ConstNode with uniform values, ArrivalRate, WipLimit): numeric input + slider. Slider range: `max(0, default × 0.1)` to `max(default × 3, default + 10)`, step = `max(default / 100, 0.1)`.
   - **Vector parameters** (ConstNode with varying values): read-only display showing the default values (editing is out of scope for this milestone).
   - **Parameter ID** displayed as the label. **Default value** shown as a small annotation. **Kind** shown as a colored badge (ConstNode / ArrivalRate / WipLimit).

4. **AC-4: Reactive series stores.** Each series from the compile/eval response is held in a Svelte writable store (or `$state` rune). When `eval` returns updated series, the stores are updated atomically and the UI re-renders.

5. **AC-5: Live eval on parameter change.** Dragging a slider or typing in a numeric input triggers `session.eval({ paramId: newValue })`. The returned series update the stores. The full current override set is sent on every eval (not just the changed one), so the state is always consistent.

6. **AC-6: Debounced slider updates.** During a continuous slider drag, eval requests are debounced to fire at most every 50ms. The final value after drag-end always triggers an eval to ensure the displayed state is accurate. Numeric-input changes (via typing) are debounced to 150ms.

7. **AC-7: Loading and error states.** While an `eval` is in flight, the parameter panel shows a subtle loading indicator (spinner next to the active control, or a top progress bar). If an eval returns an error, the error message is displayed in an alert banner and the UI remains interactive. The old series values stay visible until the error is resolved.

8. **AC-8: Reset button.** A "Reset to defaults" button resets all parameter controls to their defaults (from the parameter schema) and triggers a fresh eval.

9. **AC-9: Series display.** For every non-internal series in the eval response, the UI shows:
   - The series name
   - The current values as a compact numeric list (e.g., `[10.0, 10.0, 10.0, 10.0]`)
   - A minimal inline sparkline (simple SVG path, not a full chart library) showing the shape over time
   The sparkline's y-axis auto-scales to the series' min/max. This is intentionally lightweight — full charting is M-020.

10. **AC-10: Latency display.** The page shows the elapsed_us from the most recent eval response, prominently (e.g., "Last eval: 42 µs"). This is the "look how fast" badge of honor.

11. **AC-11: Disconnect recovery.** If the WebSocket drops (e.g., API restarts), the next parameter change triggers automatic reconnect + replay (already handled by EngineSession). The UI shows a brief "reconnecting…" indicator, then resumes normal operation. No page reload required.

12. **AC-12: Unit tests for debounce + control generation.** Vitest unit tests cover:
   - Debounce helper: multiple rapid calls collapse to one, trailing edge fires
   - Control config derivation: `ParamInfo` → `{ type, min, max, step, initial }`
   These are pure functions with no DOM dependencies.

## Technical Notes

### Debounce helper

Write a simple `createDebouncer(ms)` utility that returns `{ schedule, flush, cancel }`. Use `setTimeout` + `clearTimeout`. Keep it in `ui/src/lib/utils/debounce.ts` as a shared primitive. Unit tests with `vi.useFakeTimers()`.

### Parameter control config

A pure function:

```ts
export function paramControlConfig(param: ParamInfo): ControlConfig {
    if (Array.isArray(param.default)) return { type: 'vector', values: param.default };
    const d = param.default;
    const min = Math.max(0, d * 0.1);
    const max = Math.max(d * 3, d + 10);
    const step = Math.max(d / 100, 0.1);
    return { type: 'scalar', min, max, step, initial: d };
}
```

Keep this in `ui/src/lib/api/param-controls.ts`. Test as pure function.

### Slider component

shadcn-svelte doesn't have `slider` installed yet. Add via `pnpm dlx shadcn-svelte@latest add slider` or implement a minimal native `<input type="range">` wrapped in a Svelte component. Prefer the native approach for this milestone — one dependency less to wrangle, and full control over reactivity.

### Session lifecycle

Create a single `EngineSession` instance per page load. Store it in a `$state` or a module-level variable (carefully — SvelteKit SSR wants client-only code). Close on `onDestroy` to kill the subprocess.

### Built-in models

Keep the model YAML strings in `ui/src/lib/api/example-models.ts`. Three models:
1. **Simple pipeline**: const arrivals + expr served = arrivals × 0.8 (4 bins)
2. **Queue with WIP**: serviceWithBuffer topology, WIP limit 50, scalar inflow/outflow (6 bins)
3. **Class decomposition**: traffic.arrivals with 2 classes, served = MIN(arrivals, 8) (4 bins)

### Sparkline

No chart library dependency. A 120×30 SVG `<path>` with a single polyline drawn from min/max-normalized values. ~20 lines of Svelte.

## Out of Scope

- Full interactive charts with hover, zoom, legends (M-020)
- Topology graph visualization with heatmap (M-020)
- Loading models from URL, file upload, or template library (future)
- Editing vector parameters (editing per-bin const values) — show read-only
- Saving/loading parameter snapshots (future)
- Multi-model comparison views (future)
- Authentication, user sessions, or per-user state
- Model editing / YAML textarea — use the 3 built-ins only

## Key References

- `ui/src/lib/api/engine-session.ts` — EngineSession WebSocket client (M-018)
- `ui/src/routes/engine-test/+page.svelte` — smoke test pattern to build on
- `ui/src/lib/components/app-sidebar.svelte` — existing sidebar nav
- `ui/src/lib/components/ui/input/` — shadcn input primitive
- `work/epics/E-17-interactive-what-if-mode/spec.md` — epic context
- `docs/architecture/headless-engine-architecture.md` — WebSocket data flow

## Success Indicator

Drag a slider in the parameter panel → watch the numeric series values update in real time, with `Last eval: <N> µs` staying under 100 µs. That's the interactive what-if experience working.
