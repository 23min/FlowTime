# FT-M-05.07 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Status remains 📋 Planned until coding starts; update to 🔄 In Progress once Phase 1 implementation begins.

**Milestone:** FT-M-05.07 — Topology UI Input/Paint/Data Separation  
**Started:** 2025-12-17  
**Status:** 🔄 In Progress  
**Branch:** `milestone/ft-m-05.07`  
**Assignee:** Codex (paired with user)

---

## Quick Links

- **Milestone Document:** [`docs/milestones/FT-M-05.07-ui-perf-lanes.md`](../milestones/FT-M-05.07-ui-perf-lanes.md)
- **Architecture Epic:** [`docs/architecture/ui-perf/README.md`](../../architecture/ui-perf/README.md)
- **Milestone Guide:** [`docs/development/milestone-documentation-guide.md`](../../development/milestone-documentation-guide.md)

---

## Current Status

### Overall Progress
- [ ] Phase 1: Input Lane Strictness & Diagnostics (🔄 1/4 tasks actively in progress)
- [ ] Phase 2: Scene vs. Overlay Payload Split (0/4 tasks)
- [ ] Phase 3: Data Lane & Inspector Hygiene (0/4 tasks)
- [ ] Phase 4: Spatial Index & Final Validation (0/4 tasks)

### Test Status
- **dotnet build:** ✅ (2025-12-17)
- **dotnet test --nologo:** ✅ (2025-12-17) — perf benchmark suite skipped as expected (M2 perf tests, FlowTime.Sim smoke)
- **Playwright/Perf Tests:** ⏳ Planned for Phase 1 Task 1.2

---

## Progress Log

### 2025-12-17 - Phase 1 Task 1.1 Diagnostics Wiring

**Task 1.1 RED → GREEN:**
- ✅ Added `DiagnosticsFileWriterTests.HoverRow_ToCsvLine_IncludesExtendedMetrics` covering new CSV columns (scene rebuilds, overlay updates, layout reads, pointer INP sample/avg/max).
- ✅ Updated JS HUD + diagnostics payload to track/reset new counters (scene/layout stats, pointer INP) and surface them through the diagnostics HUD and upload payload.
- ✅ Propagated schema changes through `HoverDiagnosticsRequest`, API `Program.cs`, and CSV header writer so the new metrics persist server-side.
- ✅ Authored Playwright automation plan (`docs/performance/FT-M-05.07/playwright-plan.md`) detailing scenarios, metrics, file layout, npm scripts, and CI expectations (prereq for Task 1.2 RED).
- ✅ Scaffolded Playwright workspace (`package.json`, `tests/ui/playwright.config.ts`, helper utilities, and initial failing spec) plus npm install; diagnostics automation doc added under `docs/performance/FT-M-05.07/automation.md`. `node_modules/` is gitignored; `npm run test-ui:install` installs Chromium in the dev container.

**Validation:**
- ✅ `dotnet build`
- ✅ `dotnet test --nologo` (known perf and smoke tests skipped by design; no failures)
- 🟥 `npm run test-ui` (fails as expected today because FlowTime UI wasn’t running + latency thresholds are still RED; see `docs/performance/FT-M-05.07/automation.md`)

### 2025-12-17 - Phase 1 Task 1.2 Harness Wiring

**Highlights:**
- Added developer instructions in `docs/performance/FT-M-05.07/README.md` (stack prerequisites, npm commands, artifact locations).
- Documented status in `docs/performance/FT-M-05.07/automation.md` (browser install, expected RED failure, next CI steps).
- Verified Playwright install command (`npm run test-ui:install`) pulls Chromium into the dev container; `npm run test-ui` currently fails early because FlowTime.UI was not running, which is expected for RED.

### 2025-12-17 - Phase 1 Task 1.3 Hover RAF Refactor (step 1)

**Changes:**
- Updated `queueHoverUpdate`/`processQueuedHover` to implement a strict RAF pipeline (latest snapshot wins, no inline processing, queue drop counter reflects skipped samples).
- Hover/pan pointer stats now distinguish `received` vs. `processed` accurately, improving diagnostics fidelity ahead of Playwright enforcement.
- Ran `dotnet build` + `dotnet test --nologo`; first test run surfaced a transient `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance` failure (expected flake), and the second full run succeeded (perf suite still skipped the other PMF cases as before).
- `npm run test-ui` (Playwright) now succeeds when FlowTime.UI/API/Sim run inside the dev container; as expected the RED spec still verifies the latency thresholds and will fail once we tighten them later.

### 2025-12-17 - Phase 1 Task 1.3 Hover RAF Refactor (step 2)

**Changes:**
- Settings-button hover/drag now flows through `queueHoverUpdate`, so pointer samples taken near the settings chip respect the RAF throttle and no longer redraw synchronously.
- Drag release + delayed resume logic was hardened: `scheduleDelayedResume` queues at most one RAF, and resume snapshots reuse the cached canvas rect (no repeated `getBoundingClientRect` per event).
- Verified Playwright RED spec (`npm run test-ui`) passes when the FlowTime stack is running, and `dotnet build`/`dotnet test --nologo` remain green.

**Next:** Finish Task 1.3 by extracting shared pointer-to-canvas helpers (refactor), then move to Task 1.4 (hot-path logging toggle).

### 2025-12-17 - Kickoff & Planning

**Preparation:**
- [x] Read milestone document and UI-perf epic
- [x] Created branch `milestone/ft-m-05.07`
- [x] Created tracking document
- [x] Begin Phase 1 (diagnostics extensions in progress)

**Next Steps:**
- [ ] Implement Phase 1 Task 1.1 diagnostics extensions (RED first)
- [ ] Draft Playwright plan for input latency checks

---

## Phase 1: Input Lane Strictness & Diagnostics

**Goal:** Enforce RAF-only pointer/hover/pan handling, remove hot-path logging, and extend diagnostics/automation so input latency targets are measurable.

### Task 1.1: Extend Diagnostics HUD/CSV Schema
**File(s):** `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor`, `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`, `docs/performance/FT-M-05.07/*`

**Checklist (TDD Order):**
- [x] RED: Update diagnostics snapshot tests (new schema fields for scene/overlay counts, pointer INP) and HUD rendering tests
- [x] GREEN: Implement HUD counters + CSV writers + server DTO updates
- [ ] REFACTOR: Clean up duplication between HUD panel and CSV append logic

**Status:** 🟡 In Progress (Refactor pending)

### Task 1.2: Playwright/Automation Harness for Input Latency
**File(s):** `tests/FlowTime.UI.Tests/Playwright/*` (new)

- [x] RED: Add Playwright spec that fails if pointer INP > 200 ms or overlay updates > 1/frame during hover/pan/scrub
- [x] GREEN: Wire Playwright into repo scripts + docs (npm scripts + install command; developer instructions captured in `docs/performance/FT-M-05.07/README.md` and `automation.md`)
- [x] REFACTOR: Document how to run the suite in `docs/performance/FT-M-05.07/README.md`

**Status:** ✅ Done (CI hook still to be addressed in later phase, but dev workflow + docs complete)

### Task 1.3: RAF-only Input Handling
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`

- [ ] RED: JS unit/perf harness showing multiple hover updates per frame
- [ ] GREEN: Introduce single RAF queue per gesture, cached `getBoundingClientRect()`, and latest sample semantics
- [ ] REFACTOR: Shared helper for pointer-to-canvas coordinate checks

**Status:** ⏳ Not Started

### Task 1.4: Remove Hot-Path Logging + Debug Flag
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

- [ ] RED: Add failing test/logging assertion showing existing logs fire during hover/pan
- [ ] GREEN: Replace with guarded debug flag toggled from HUD settings
- [ ] REFACTOR: Document debug workflow in README

**Status:** ⏳ Not Started

### Phase 1 Validation
- [ ] `dotnet build`
- [ ] `dotnet test --nologo`
- [ ] `npx playwright test` (new suite)
- [ ] Manual Chrome trace confirming pointer INP < 200 ms per spec

---

## Phase 2: Scene vs. Overlay Payload Split

**Goal:** Rebuild scene geometry only when topology structure changes; bin/hover updates use lightweight overlay deltas.

### Task 2.1: Scene & Overlay Signatures
**File(s):** `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor`, `TopologyCanvasBase`

- [ ] RED: Add unit tests verifying bin-only changes do not trigger `renderScene`
- [ ] GREEN: Implement `sceneSignature`/`overlaySignature` tracking and JS interop separation (`renderScene` vs. `applyOverlayDelta`)
- [ ] REFACTOR: Share snapshot structures between .NET and JS

### Task 2.2: JS Renderer Delta Pipeline
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`

- [ ] RED: Add JS tests ensuring overlay deltas update highlights without rerender
- [ ] GREEN: Implement `FlowTime.TopologyCanvas.applyOverlayDelta` and DOM updates
- [ ] REFACTOR: Consolidate hover/selection overlay updates

### Task 2.3: DOM Proxy Update Hygiene
**File(s):** `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor`

- [ ] RED: Add component/unit test ensuring proxies are not rebuilt on bin change
- [ ] GREEN: Update proxies in place (attributes/style) for focus/dim states
- [ ] REFACTOR: Document keyboard focus behavior

### Task 2.4: Diagnostics for Scene vs. Overlay Counts
**File(s):** HUD/CSV + docs

- [ ] RED: HUD tests expect new counters
- [ ] GREEN: Emit scene and overlay delta counts per bin change and include in CSV dumps
- [ ] REFACTOR: Document thresholds in milestone validation protocol

### Phase 2 Validation
- [ ] HUD capture showing zero scene rebuilds during scrubs
- [ ] Playwright assertion verifying overlay deltas only during bin change
- [ ] Manual regression: hover/pan unaffected by scene cache

---

## Phase 3: Data Lane & Inspector Hygiene

**Goal:** Keep heavy metric recompute off the input lane, gate inspector work, and ensure persistence happens via debounced saves.

### Task 3.1: Async Metric Delta Computation
**File(s):** `Topology.razor`, supporting services

- [ ] RED: Unit test ensuring bin recompute can be cancelled + resumed
- [ ] GREEN: Refactor to produce delta payloads (per-node metrics) via cancellable background tasks
- [ ] REFACTOR: Share context snapshot builder between scrubber and inspector

### Task 3.2: Inspector-only Calculations Gating
**File(s):** `Topology.razor`, inspector components

- [ ] RED: Tests verifying inspector-closed scenarios skip expression/class computations
- [ ] GREEN: Implement gating and fallback data for canvas overlay
- [ ] REFACTOR: Document inspector perf expectations

### Task 3.3: Debounced State Persistence
**File(s):** `Topology.razor`

- [ ] RED: Add tests ensuring `localStorage` writes are debounced during scrubs/pans
- [ ] GREEN: Implement debounced save + validation
- [ ] REFACTOR: Update docs on persistence intervals

### Task 3.4: Playwright Coverage for Inspector On/Off
**File(s):** Playwright suite

- [ ] RED: Extend automation to cover inspector toggle scenario
- [ ] GREEN: Ensure suite passes with new gating
- [ ] REFACTOR: Document run steps

### Phase 3 Validation
- [ ] HUD capture showing overlay deltas <= 1/frame with inspector open
- [ ] Playwright suite passing
- [ ] `dotnet test --nologo`

---

## Phase 4: Spatial Index & Final Validation

**Goal:** Reduce edge hit-test cost via world-space grid/quadtree and finalize perf validation per milestone spec.

### Task 4.1: World-Space Grid Implementation
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`

- [ ] RED: JS perf test showing current O(E) hit-test cost
- [ ] GREEN: Implement grid/quadtree index and rebuild logic
- [ ] REFACTOR: Make cell size configurable

### Task 4.2: Cached Pointer Hit Results
**File(s):** same as above

- [ ] RED: Test verifying repeated samples within epsilon reuse cached edge
- [ ] GREEN: Implement caching logic, fallback when pointer exits cell
- [ ] REFACTOR: Document cache invalidation rules

### Task 4.3: Diagnostics & HUD Updates
**File(s):** HUD/CSV docs

- [ ] RED: HUD tests expecting spatial index counters
- [ ] GREEN: Emit counts and thresholds; update docs
- [ ] REFACTOR: Summaries in `docs/performance/FT-M-05.07`

### Task 4.4: Final Validation Runs
**File(s):** `docs/performance/FT-M-05.07/*`, release notes

- [ ] RED: N/A (manual validation) — plan documented
- [ ] GREEN: Capture Chrome trace + HUD for full & operational modes (inspector open/closed)
- [ ] REFACTOR: Finalize release note + update milestone status

### Phase 4 Validation
- [ ] Chrome traces show pointer INP < 200 ms and scene rebuild count = 0
- [ ] HUD CSVs stored per protocol
- [ ] `dotnet build`, `dotnet test --nologo`

---

## Testing & Validation Checklist

- [ ] `dotnet build`
- [ ] `dotnet test --nologo`
- [ ] `npx playwright test` (with new suites)
- [ ] Manual Chrome trace (full + operational)
- [ ] HUD/CSV capture stored under `docs/performance/FT-M-05.07`

---

## Issues Encountered

_(None yet — populate as work progresses)_

---

## Final Checklist (to be completed at milestone wrap-up)

### Code Complete
- [ ] All phase tasks complete
- [ ] All tests passing
- [ ] No compilation errors or runtime warnings
- [ ] Code reviewed (if applicable)

### Documentation
- [ ] Milestone document updated to ✅ Complete
- [ ] Tracking doc finalized
- [ ] Release note added (`docs/releases/FT-M-05.07*.md`)
- [ ] Performance artifacts stored

### Quality Gates
- [ ] Unit/integration tests passing
- [ ] Playwright/perf tests passing
- [ ] Manual validation approved
- [ ] Diagnostics numbers meet thresholds
