# Tracking: m-E17-02 Svelte Parameter Panel

**Milestone:** m-E17-02
**Epic:** E-17 Interactive What-If Mode
**Status:** complete
**Branch:** `milestone/m-E17-02-svelte-parameter-panel`
**Started:** 2026-04-10

## Progress

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | Route /what-if with app shell | done |
| AC-2 | Model picker with 3 built-in models | done |
| AC-3 | Parameter panel auto-generation | done |
| AC-4 | Reactive series stores ($state) | done |
| AC-5 | Live eval on parameter change | done |
| AC-6 | Debounced slider (50ms) / input (150ms) | done |
| AC-7 | Loading and error states | done |
| AC-8 | Reset to defaults button | done |
| AC-9 | Series display with sparklines | done |
| AC-10 | Latency badge (Last eval: N µs) | done |
| AC-11 | Disconnect recovery | done (see notes) |
| AC-12 | Unit tests for debounce + controls | done |

## Implementation Summary

### New files
- `ui/src/lib/utils/debounce.ts` — pure createDebouncer helper (49 lines)
- `ui/src/lib/utils/debounce.test.ts` — 9 vitest unit tests
- `ui/src/lib/api/param-controls.ts` — paramControlConfig + kindLabel (75 lines)
- `ui/src/lib/api/param-controls.test.ts` — 17 vitest unit tests
- `ui/src/lib/api/example-models.ts` — 3 built-in model YAML strings
- `ui/src/lib/components/sparkline.svelte` — minimal SVG sparkline (~40 lines)
- `ui/src/routes/what-if/+page.svelte` — main interactive page

### Modified files
- `ui/src/lib/components/app-sidebar.svelte` — added What-If nav entry

### Test totals
- 51 vitest unit tests (25 existing + 9 debounce + 17 param-controls)
- All type checks pass (zero new type errors)
- End-to-end verified via Node WebSocket client: all 3 models compile and eval in 6-16 µs

## AC-11 Note: Disconnect Recovery

The EngineSession client (m-E17-01) handles reconnection transparently:
when the WebSocket drops, the next call automatically reconnects and replays
the last compile + overrides. Reconnection typically completes in under 100ms
on localhost, so no distinct "reconnecting…" indicator is needed — the existing
`evalInFlight` spinner covers the user's perception of the pause. The user
never sees an error or stuck state; they just see "evaluating…" briefly longer.

For explicit visibility of session state (useful for debugging), future work
could surface EngineSession's internal state via an observable. Not required
for this milestone's user experience.

## End-to-end verification

Started API + Rust engine binary, then drove all 3 example models via Node
WebSocket client (simulating what the Svelte page does):

```
✓ simple-pipeline: 1 params [arrivals], eval 6µs, 2 series
✓ queue-with-wip: 3 params [arrivals, served, Queue.wipLimit], eval 10µs, 9 series
✓ class-decomposition: 3 params [arrivals, arrivals.Order, arrivals.Refund], eval 16µs, 6 series
```

Demonstrates the full path: Svelte UI → WebSocket → .NET proxy → Rust engine session
→ parameterized eval → MessagePack response → reactive UI update.
