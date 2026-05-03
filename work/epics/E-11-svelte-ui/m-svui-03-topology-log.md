# Tracking: m-svui-03-topology

**Started:** 2026-03-30
**Completed:** 2026-03-30
**Commit:** `457018b`

## Progress

| AC | Status | Notes |
|----|--------|-------|
| Selecting a run renders topology SVG | done | dag-map bezier routing, port-stripped edges |
| Dark/light theme updates graph | done | Custom themes with transparent paper, shadcn-matched colors |
| All route stubs render | done | No regressions |

## Decisions

- Used dag-map instead of topologyCanvas.js (ADR-SVUI-02 superseded)
- dag-map added as local dependency (`pnpm add ../lib/dag-map`)
- Node kind mapped to single `cls: 'core'` — class differentiation deferred until API provides route data
- Bezier routing chosen over angular for cleaner look at this graph size
- topologyCanvas.js NOT copied to Svelte UI — stays in Blazor only

## Known Gaps

- Trunk is "wiggly" — dag-map layout engine assigns branch Y-positions that push the trunk around. Layout quality improvement needed in dag-map.
- No per-node metric coloring — needs dag-map heatmap mode (M4 dependency)
- No click/hover interactivity — needs dag-map event support (M5 dependency)
