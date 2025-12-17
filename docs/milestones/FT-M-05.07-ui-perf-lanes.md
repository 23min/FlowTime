# FT-M-05.07 — Topology UI Input/Paint/Data Separation

**Status:** 📋 Planned  
**Epic Reference:** `docs/architecture/ui-perf/README.md`  
**Owner:** FlowTime UI  
**Scope:** FlowTime.UI (Time-Travel Topology page + diagnostics)  
**Branches:** Create `milestone/ft-m-05.07` from `milestone/ft-m-05.06` once this spec is approved.  
**Tracking Doc:** `docs/milestones/tracking/FT-M-05.07-tracking.md` (create when work starts).  

## Overview

FT-M-05.07 formalizes the UI performance architecture for the topology page. FT-M-05.06 delivered the diagnostics HUD, JS hover ownership, and timeline drag gating. The remaining work is to separate input, paint, and data lanes so pointer interactions never block on scene rebuilds, even when graph payloads grow. This milestone implements the “scene vs. overlay” split, throttles Blazor refreshes, and moves heavy recomputations off the hot path per the UI-perf epic.

### Motivation

- Pointer/pan/hover latency still spikes on large runs because scene build, paint, and metric recompute share the same main-thread pipeline.  
- Bin changes rebuild the entire `CanvasRenderRequest`, causing DOM/JS churn even when geometry is static.  
- Edge hit-tests and overlay transforms still run inline with pointermove, risking missed events.  
- We need a structured foundation before we can consider larger DAG bundles or WASM shifts.

## Dependencies

1. ✅ FT-M-05.06 — diagnostics HUD, scrub gating, and operational toggle resiliency are required so we can measure and regress improvements.  
2. No other blocking milestones; this work happens on top of `milestone/ft-m-05.06`.  

## In Scope ✅

- Implement the three-lane architecture (input, paint, data) described in `docs/architecture/ui-perf/README.md`.  
- Split the topology render payload into static “scene” and dynamic “overlay” parts; JS must repaint without rehydrating geometry on every bin change.  
- Ensure pointer/pan/hover interactions are RAF-coalesced with cached layout reads.  
- Add world-space spatial indexing for edge hit-testing.  
- Reduce Blazor/DOM churn by updating metrics/inspector data only when necessary.  
- Extend diagnostics HUD/CSV capture with new counters (scene rebuilds vs. overlay updates, pointer INP).  

## Out of Scope ❌

- Engine/runtime performance optimizations (router evaluation, etc.).  
- Rewriting the topology renderer from scratch (Canvas/DOM stack remains).  
- New template changes or pipeline adjustments outside the topology UI.  

## Artifacts & Deliverables

1. Updated topology UI implementation (`src/FlowTime.UI/...`) with scene/overlay split and input-lane guards.  
2. Diagnostics HUD + CSV schema updates documenting scene rebuild vs. overlay updates.  
3. Playwright or equivalent automation covering hover/pan/scrub latency budget.  
4. Release notes (`docs/releases/FT-M-05.07-*.md`) and milestone tracking doc updates.  

## Measurements & TDD Strategy

We continue RED → GREEN → REFACTOR for every new behavior:

1. **Instrumentation First (RED):** Extend diagnostics HUD/CSV schema to expose scene rebuild counts, overlay delta counts, pointer drop rate, and INP marks. Add Playwright/JS harness assertions that fail if these metrics exceed thresholds.  
2. **Implementation (GREEN):** Apply input/pain/data separation tasks, ensuring new tests/metrics pass locally.  
3. **Refactor:** Once behavior is covered, clean up shared helpers (e.g., overlay update pipeline, spatial index caching).

Tests must precede implementation for each phase (e.g., failing Playwright test for per-frame overlay updates, failing unit test for scene signature caching). Use:

- `dotnet test --nologo` (full solution).  
- `npm run test-ui` or Playwright suite (recorded in tracking doc).  
- Chrome trace/HUD captures stored under `docs/performance/FT-M-05.07/...`.

## Phase Breakdown

### Phase 1 — Input Lane Strictness & Diagnostics

**Goal:** Ensure pointer, hover, and pan code paths are RAF-coalesced and measurable.

- **Task 1.1:** Extend diagnostics HUD/CSV schema with counters for scene rebuilds, overlay deltas, pointer INP, and layout reads per frame.  
- **Task 1.2:** Add Playwright smoke test: simulate hover/pan/scrub, fail if pointer INP > 200 ms or overlay updates exceed 1/frame.  
- **Task 1.3:** Refactor `topologyCanvas.js` input handlers to guard every pointermove with RAF scheduling, cached `getBoundingClientRect()`, and `latestSample` semantics.  
- **Task 1.4:** Remove any remaining hot-path logging and expose a debug flag (HUD toggle) to view counts without DevTools.

**Exit Criteria:** Diagnostics show stable pointer INP < 200 ms while hovering/panning, and Playwright automation passes.

### Phase 2 — Scene vs. Overlay Payload Split

**Goal:** Only rebuild the “scene” when topology structure changes; bin/hover updates apply as lightweight deltas.

- **Task 2.1:** Introduce `sceneSignature` and `overlaySignature` in `TopologyCanvasBase`: geometry payload changes only when filters/layout change.  
- **Task 2.2:** Update JS renderer with `FlowTime.TopologyCanvas.renderScene(scenePayload)` and `applyOverlayDelta(deltaPayload)`. Delta includes hovered/focused IDs, per-node metric colors, warnings, and diagnostics overlay data.  
- **Task 2.3:** Debounce/delay DOM proxy rebuilds; update node proxy attributes in place for focus/selection when possible.  
- **Task 2.4:** Write unit/integration coverage ensuring bin-only changes call the overlay path and scene payload remains untouched.  

**Exit Criteria:** Diagnostics show scene rebuild count remains zero during scrubs/pans; overlay delta count matches bin changes. HUD/CSV captures verify improvements.

### Phase 3 — Data Lane (Metrics & Inspector Hygiene)

**Goal:** Keep heavy metric recompute off the input/pain lanes and minimize Blazor churn.

- **Task 3.1:** Split metric recompute pipeline into a Worker-style async helper (`Task.Run` in .NET) that produces overlay deltas; ensure background work yields frequently and is cancellable.  
- **Task 3.2:** Gate inspector-specific calculations (class contribution charts, expression metadata) so they run only when inspector is open. Provide fallback data (cached slices) for the canvas overlay when inspector is closed.  
- **Task 3.3:** Persist `selectedBin`, `visibleStartBin`, and viewport changes via debounced saves to avoid thrashing `localStorage`.  
- **Task 3.4:** Update Playwright/perf tests to verify inspector toggling no longer impacts hover latency; record results in tracking doc.

**Exit Criteria:** Diagnostics show overlay delta updates staying under 1/frame even with inspector open; no main-thread long tasks >50 ms during hover/pan.

### Phase 4 — Edge Hit-Test Spatial Index & Final Validation

**Goal:** Make hover detection scale sub-linearly with node/edge count and finalize perf validation.

- **Task 4.1:** Implement a world-space uniform grid (configurable cell size) for edge hitboxes in JS; rebuild only when scene changes.  
- **Task 4.2:** Cache last-hit edge/node per cell and only re-run distance calculations if pointer exits the cached cell or exceeds epsilon.  
- **Task 4.3:** Add diagnostics counters for spatial index hits vs. fallbacks; capture before/after data in `docs/performance/FT-M-05.07`.  
- **Task 4.4:** Final Chrome trace + HUD run following the validation protocol (full + operational modes, inspector open/closed) and store results.

**Exit Criteria:** Spatial index reduces edge candidates to ≤8 per hover sample on transportation run; Chrome traces show no pointer stalls; HUD data meets thresholds (interop dispatch ≤0.5/s when inspector closed, pointer drops ≤5%, draw avg ≤6 ms).

## Final Definition of Done

- ✅ Diagnostics HUD/CSV capture scene rebuilds vs. overlay updates, pointer INP, and spatial index stats; docs reference storage paths.  
- ✅ Playwright/perf automation passes, ensuring input-lane targets (pointer INP < 200 ms, overlay 1/frame) hold.  
- ✅ Scene payload is rebuilt only when topology structure changes; bin/hover use overlay deltas.  
- ✅ Metrics/inspector recompute stays off the input lane, with inspector gating.  
- ✅ Spatial index reduces edge hit-test cost and hover remains frame-rate even at max graph size.  
- ✅ Milestone tracking doc reflects RED→GREEN→REFACTOR steps and measurement outcomes.  
- ✅ Release note (`docs/releases/FT-M-05.07-*.md`) summarizes impact and references diagnostics captures.

## Risks & Open Questions

- **Worker vs. Task.Run:** We’ll start with `Task.Run` in .NET; if GC pressure persists, future work may move metric recompute to a JS worker. Document findings in tracking doc.  
- **Playwright env variance:** If CI vs. local performance differs, document thresholds per environment and gate tests accordingly.  
- **Spatial index tuning:** Cell size might need runtime configuration; initial implementation should make it configurable or adaptive.  

## Next Steps After This Milestone

- Evaluate whether dag bundle loading (selective vs. bulk) should adopt Worker-based hydration (separate epic).  
- Consider inspector redesign or virtualization if DOM overlay remains heavy after this milestone.  
- Revisit diagnostics HUD UX for non-dev builds.
