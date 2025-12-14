# FT-M-05.06 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](milestone-rules-quick-ref.md) for workflow.

**Milestone:** [FT-M-05.06 — Topology Canvas Performance Sweep](../FT-M-05.06-performance.md)  
**Started:** 2025-12-06  
**Status:** 🚧 In Progress  
**Branch:** `milestone/ft-m-05.06`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** [`docs/milestones/FT-M-05.06-performance.md`](../FT-M-05.06-performance.md)
- **Related Analysis:** *(to be captured under `docs/performance/FT-M-05.06/` alongside profiling traces)*
- **Milestone Guide:** [`docs/development/milestone-documentation-guide.md`](milestone-documentation-guide.md)

---

## Current Status

### Overall Progress
- [ ] **Phase 1:** Input guardrails/throttling — core gating + RAF logic landed, but automated tests and doc polish still pending.
- [ ] **Phase 2:** JS/interop optimizations — dedupe/caching code is in, inspector batching works, but perf verification + tests still outstanding.
- [ ] **Phase 3:** Profiling & validation — instrumentation (HUD, diagnostics CSV, debug logging hooks) is ready; awaiting before/after capture per Validation Protocol.
- [ ] **Phase 4:** JS hover ownership & timeline UX — planned scope, no implementation yet.
- [ ] **Phase 5:** Main-thread latency remediation — Task 5.1 ✅ (logging), Task 5.2 ✅ (RAF coalescing), Task 5.3 ✅ (scene cache), Task 5.4 ✅ (edge spatial index), Task 5.5 ✅ (viewport debounce & rect caching).

### Test Status
- **Unit Tests:** 0 passing / 0 total
- **Integration Tests:** 0 passing / 0 total
- **E2E Tests:** 0 passing / X planned

---

## Progress Log

### 2025-12-06 — Kickoff

**Preparation:**
- [x] Read milestone document
- [x] Review branching strategy / guardrails
- [x] Create milestone branch `milestone/ft-m-05.06`
- [ ] Capture baseline Chrome trace (pending once harness ready)

**Next Steps:**
- [ ] Design hover guardrails (Phase 1 Task 1.1)
- [ ] Outline testing strategy for UI hover perf (Phase 3 prep)

### 2025-12-06 — Hover throttling scaffolding

**Changes:**
- Added hover RAF queue + pointer bounds gating in `topologyCanvas.js`.
- Reset canvas cursor when hover exits and cancel queued work on pointer leave.

**Tests:**
- Pending (need Playwright/manual perf validation once UI harness available).

**Next Steps:**
- Write automated/manual checks for hover gating.
- Continue with hit-test caching / inspector batching (Phase 2).

### 2025-12-06 — Interop dedupe + inspector debounce

**Changes:**
- Implemented hover hit-test caching + pointer reuse guardrails in `topologyCanvas.js`.
- Deduped JS→.NET hover callbacks and throttled inspector updates to ~1/frame.
- Added hover interop instrumentation + debug helpers.

**Tests:**
- Pending perf captures + UI smoke.

**Next Steps:**
- Validate caching thresholds against large runs.
- Begin instrumentation + Chrome trace capture (Phase 3 prep).

### 2025-12-06 — Diagnostics HUD & API ingest

**Changes:**
- HUD overlay with live hover/sec stats + dump chip (bottom-right of canvas).
- `FlowTime.TopologyCanvas.dumpHoverDiagnostics` + auto POST to `/v1/diagnostics/hover`.
- API writes payloads to `data/diagnostics/<runId>/hover_<timestamp>.json`.

**Tests:**
- Manual smoke: toggle `?diag=1`, verify dumps + files under `/data/diagnostics`.

**Next Steps:**
- Capture before/after counts via HUD + CLI script.

### 2025-12-11 — HUD persistence & scope update

**Changes:**
- HUD can now be collapsed to a chip, state stored in `localStorage`, and counts reset whenever users expand/dump.  
- Diagnostics defaults moved into `wwwroot/appsettings*.json`, so WASM picks them up without query flags.  
- Added `?hovercache=0` query flag for profiling (disables hit-test cache) and documented the new scope in the milestone doc (Phase 4).

**Next Steps:**
- Move node hover notifications into JS (parity with edge hover), then revisit timeline pointer lag once hover feels instant.

### 2025-12-13 — Drag resume snapshot fix & diagnostics

**Changes:**
- Captured pointer snapshots during drag start/move/up so hover resume uses the latest cursor location instead of stale coordinates.
- Ensured diagnostics hover dumps keep reporting up-to-date pointer stats post-drag.
- Added post-drag intent bypass so the first hover sample after a pan repaints immediately and no longer waits for the throttle window to elapse.
- Hooked window-level pointer up/cancel listeners and removed the extra RAF delay so drag releases trigger hover resumption even if the cursor leaves the canvas.
- Split drag “armed” vs “active” states so we no longer suspend hover/log a drag when the user simply clicks, and block all hover queues during the active drag so hovering resumes immediately on release.
- Coalesced drag pointer moves to one RAF per frame, cancelling pending work on release so pan motion no longer replays old samples for seconds after you let go. Diagnostics now log JS↔.NET hover durations, exposing Blazor-side bottlenecks, and Blazor hover callbacks are suppressed unless the inspector is visible.

**Tests:**
- Pending — will re-run `dotnet build` + `dotnet test --nologo` after batching the hover fixes.

### 2025-12-14 — Drag resume queue trim

**Changes:**
- Drag releases now resume hover sampling asynchronously (via RAF) instead of forcing synchronous `queueHoverUpdate` calls for every pan, so prior drag samples are trimmed immediately.
- Keeps the delayed resume hook for parity but ensures only the latest release snapshot gets processed so hover processing no longer blocks new drag gestures.

**Tests:**
- `dotnet build`
- `dotnet test --nologo`

**Next Steps:**
- Phase 4 Task 4.2 — suppress hover draws until the drag queue drains.
- Phase 4 Task 4.3 — move tooltip rendering entirely into JS to avoid Blazor interop churn when the inspector is closed.

### 2025-12-14 — Main-thread latency remediation plan

**Context:** Manual profiling still shows “freeze, then flush” pointer behavior on large graphs. An audit identified several hot spots that remain after the initial RAF/caching work.

**Plan Refresh:**
- Remove hot-path logging in `draw`, `render`, hover, and viewport emitters (replace with a guarded `debugLog` helper).
- Ensure drag and hover processing only update via RAF, dropping intermediate pointer samples so the latest snapshot always wins.
- Split scene rebuilds from hover-only visuals; avoid re-hydrating hitboxes/node maps when only the highlight changes.
- Add a coarse spatial index for edge hit-tests plus epsilon-based caching to keep hover cost bounded.
- Tighten interop/layout: cache `getBoundingClientRect` per frame, debounce viewport change notifications, and keep `.NET` hover callbacks suppressed unless the inspector is visible **and** the ID changes.

**Action Items:**
- [ ] Update the milestone spec (done in FT-M-05.06 doc) and expand the tracker phases.
- [ ] Capture before/after HUD snapshots once logging removal lands.
- [ ] Sequence implementation: logging → RAF enforcement → hover redraw split → hit-test index → interop/layout tuning.

### 2025-12-14 — Task 5.1: Hot-path logging removal

**Changes:**
- Introduced a persisted `debugLog` helper guarded by `state.debugEnabled` (configurable via querystring/localStorage or `FlowTime.TopologyCanvas.setDebugLoggingEnabled`), so normal sessions skip logging entirely.
- Removed every `console.*` call from drag/hover/dispatch paths; diagnostics messages now flow through `debugLog`.
- Diagnostics options can now carry an optional `debugLogging` flag for future toggling.

**Tests:**
- `dotnet build`
- `dotnet test --nologo` (reran flaky `Test_PMF_Mixed_Workload_Performance` before the full suite)

**Next Steps:**
- Move to Task 5.2 (strict RAF coalescing) now that logging noise is gone.

### 2025-12-14 — Task 5.2: Drag/Hover RAF coalescing

**Changes:**
- Ensured hover sample drops only count once per RAF tick (`hoverDropReported` flag) so metrics now reflect frames that backlog rather than every overwritten pointer event.
- Kept only one hover snapshot alive at a time and reset drop flags whenever samples flush or get cancelled, guaranteeing the latest pointer sample wins each frame without building a long queue.
- Drag RAF handling already coalesced pending moves; this change brings the hover path in line so both panning and hovering are strictly one-update-per-frame.

**Tests:**
- `dotnet build`
- `dotnet test --nologo`

**Next Steps:**
- Proceed to Task 5.3 (split hover visuals from full scene rebuild) now that event coalescing is under control.

### 2025-12-14 — Task 5.3: Scene cache + theme-safe invalidation

**Changes:**
- Split `draw` into a cached “scene” rebuild (`state.sceneDirty`) and a lightweight paint pass so hover-only redraws reuse node/edge metadata, hitboxes, and overlay legends.
- Introduced `state.collectingHitboxes` so chip/edge hitboxes only rebuild when the scene is dirty; hover draws now reuse the previous buffers without pushing duplicate hitboxes or version IDs.
- Cached nodes/edges/legacy tooltip payloads and marked the scene dirty whenever payloads arrive, payloads clear, or theme changes fire (theme observers now call `redrawActiveStates({ markSceneDirty: true })`).
- Reset tooltip/hitbox metadata only when rebuilding so hover changes no longer blow away caches, and kept theme changes in sync by forcing a rebuild when palette flips.

**Tests:**
- `dotnet build`
- `dotnet test --nologo` *(initial run failed on two `FlowTime.Tests.Performance.M15PerformanceTests` benchmarks; reran each filter individually and both passed in isolation — perf noise acknowledged)*

**Next Steps:**
- Kick off Task 5.4 (edge hit-test spatial index) and Task 5.5 (viewport/rect caching) once cached scene behavior soaks.

### 2025-12-14 — Task 5.4: Edge hit-test spatial index

**Changes:**
- Added a world-space uniform grid (`EDGE_SPATIAL_INDEX_CELL_SIZE = 256`) that indexes edge hitboxes whenever the scene is rebuilt; each hitbox now stores a bounding box and buckets itself into every overlapping cell.
- `hitTestEdge` now queries the grid based on pointer world coordinates plus tolerance, testing only the nearby candidates instead of all edges; if the grid has no entries we fall back to the legacy linear scan.
- Cached hitboxes (and the grid) persist between hover-only paints, leveraging the existing `sceneDirty` flag, and theme/payload resets now call `resetEdgeSpatialIndex` so cached geometry stays consistent.

**Tests:**
- `dotnet build`
- `dotnet test --nologo` *(initial failure: `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance` triggered the known parse-overhead flake; reran the filtered test and it passed.)*

**Next Steps:**
- Move to Task 5.5 (viewport rect caching + interop debounce) now that edge hit-tests scale sub-linearly with graph size.

### 2025-12-14 — Task 5.5: Viewport debounce + rect caching

**Changes:**
- Added a per-frame cached `getBoundingClientRect()` helper so pointer/hover/drag logic reuses one DOM measurement; invalidated the cache on canvas resize and reused it everywhere we previously called `canvas.getBoundingClientRect()`.
- Debounced `emitViewportChanged` with a 120 ms idle window so JS only notifies .NET after wheel bursts settle; drag releases and programmatic viewport changes use `{ immediate: true }` to flush instantly, and pending debounced emits are cancelled whenever an immediate change occurs.
- Wheel events now trigger viewport updates (debounced) so zoom adjustments propagate to Blazor once the gesture ends.
- Added optional debug logging (`topologyDebug=1` or `setDebugLoggingEnabled(true)`) for hover interop dispatches so we can monitor node vs. edge calls while running the validation protocol.

**Tests:**
- `dotnet build`
- `dotnet test --nologo`

**Next Steps:**
- Begin profiling to confirm the hover HUD now reflects fewer interop dispatches, then proceed toward Phase 4 or wrap-up depending on milestone scope.

---

## Phase 1: Input Guardrails & Throttling

**Goal:** Prevent unnecessary hover updates by filtering pointer events, throttling work, and skipping redundant renders.

### Task 1.1: Canvas bounds gating
**File(s):** `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor(.cs)`

**Checklist (TDD Order - Tests FIRST):**
- [ ] RED: Add JS test/diagnostic proving pointer events fire outside bounds
- [ ] GREEN: Gate hover handling on `getBoundingClientRect()` containment
- [ ] REFACTOR: Share helper for pointer-to-canvas coordinate checks

**Tests:**
- [ ] Playwright/JS harness verifying no hover state changes when cursor outside canvas

**Status:** 🚧 Implementation landed in JS; Playwright coverage + shared helper refactor still pending.

### Task 1.2: `requestAnimationFrame` throttling
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`

**Checklist:**
- [ ] RED: Record perf counter showing multiple hover calls per frame
- [ ] GREEN: Wrap hover updates in `requestAnimationFrame`
- [ ] REFACTOR: Ensure cancellation/reset logic handles canvas dispose

**Tests:**
- [ ] Manual perf trace shows ≤1 hover update per frame

**Status:** 🚧 RAF queue + cancellation code committed; perf trace + disposal guards outstanding.

### Task 1.3: Skip redundant redraws
**File(s):** `TopologyCanvas.razor(.cs)`, JS hover helper

**Checklist:**
- [ ] Detect when computed hover ID matches previous value
- [ ] Bypass interop/invalidate paths when nothing changed
- [ ] Confirm inspector hover highlights still update correctly

**Tests:**
- [ ] UI smoke verifying hover chips still respond when moving between edges

**Status:** 🚧 JS + Blazor handlers short-circuit duplicate IDs; instrumentation counter + tests still needed.

---

## Phase 2: JS/Interop Optimizations

**Goal:** Reduce .NET interop churn by deduplicating events, caching hit-tests, and batching inspector updates.

### Task 2.1: De-duplicate `OnEdgeHoverChanged`
**File(s):** `TopologyCanvas.razor(.cs)`, `topologyCanvas.js`

**Checklist:**
- [ ] Track last hovered edge ID in JS and .NET
- [ ] Avoid null→null and same-ID calls into .NET
- [ ] Add logging counter (behind debug flag) to confirm reductions

**Status:** 🚧 Logic implemented; need perf verification + debug counter to close the task.

### Task 2.2: Hit-test caching
**File(s):** `topologyCanvas.js`

**Checklist:**
- [ ] Cache last pointer position and bounding boxes
- [ ] Reuse cached hit test results when pointer movement < threshold
- [ ] Expose knob for sensitivity if needed

**Status:** 🚧 Cache + debounce in place; inspector parity tests outstanding.

### Task 2.3: Debounce inspector updates
**File(s):** `TopologyInspector.razor`, state services

**Checklist:**
- [ ] Introduce debounce or RAF batching for inspector state updates
- [ ] Ensure un-hover still clears state promptly
- [ ] Validate no stale selections remain when switching nodes quickly

**Status:** 🚧 Debounce implemented with instrumentation hooks; before/after counts + documentation pending.

---

## Phase 5: Main-Thread Latency Remediation

**Goal:** Eliminate remaining “freeze, then flush” behavior by cutting hot-path overhead, bounding hit-test cost, and keeping interop/layout work off the critical path.

### Task 5.1: Strip hot-path logging
**Files:** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`

- Remove `console.*` calls from `draw`, `render`, viewport emitters, hover/drag handlers.
- Add a `debugLog(state, ...args)` helper and guard existing diagnostics behind a `state.debugEnabled` flag.
- Verify HUD/reporting still works without noisy logs.

**Status:** ✅ Completed 2025‑12‑14 — logging removed, `debugLog` + runtime toggle verified via build/test runs.

### Task 5.2: Enforce RAF coalescing for drag/hover
**Files:** `topologyCanvas.js`

- Ensure pointermove while dragging only schedules one RAF, and pending snapshots always overwrite previous events.
- Apply the same logic to hover updates (queue once per frame, trim intermediate samples).
- Update diagnostics counters to confirm pointer queue drops fall near zero.

**Status:** ✅ Completed 2025‑12‑14 — hover queue now keeps one sample per RAF, drop metrics track per-frame overruns.

### Task 5.3: Split hover visuals from full scene rebuild
**Files:** `topologyCanvas.js`

- Separate “rebuild hitboxes/nodeMap” from “paint using cached data”.
- Short-circuit `draw` when `hasHoverVisualDelta` is false.
- Add a lightweight overlay pass for hover-only highlights if needed.

**Status:** ⏳ Not Started — pending after RAF work stabilizes.

### Task 5.4: Edge hit-test index & caching
**Files:** `topologyCanvas.js`

- Build a coarse grid/quadtree for edge hitboxes to prefilter candidates.
- Cache last edge result when pointer moves less than the world epsilon.
- Document the new structure in the diagnostics logs (edge candidates per sample).

**Status:** ⏳ Not Started — design pending (world-space grid vs quadtree).

### Task 5.5: Interop/layout hygiene
**Files:** `topologyCanvas.js`, `TopologyCanvas.razor.cs`

- Cache `getBoundingClientRect()` per RAF tick; refresh only on resize/scroll.
- Debounce `emitViewportChanged` so .NET only hears about drags/wheels when the gesture completes.
- Double-check `.NET` hover callbacks remain suppressed unless the inspector is visible and IDs change.
- Update diagnostics CSV/HUD with before/after readings.

**Status:** ⏳ Not Started — will follow once 5.3/5.4 are in place.

## Phase 3: Profiling & Validation

**Goal:** Capture before/after evidence and ensure UX remains correct.

### Task 3.1: Chrome trace capture
**File(s.):** `docs/performance/FT-M-05.06/*`

**Checklist:**
- [ ] Record baseline trace on transportation run
- [ ] Record post-fix trace highlighting reduced scripting time
- [ ] Document instructions + screenshots

**Status:** 🚧 In Progress — `/docs/performance/FT-M-05.06/README.md` documents the workflow; need actual before/after traces + screenshots.

### Task 3.2: Hover interop instrumentation
**File(s.):** `topologyCanvas.js`, optional metrics helper

**Checklist:**
- [ ] Add optional counter/log (dev builds) for hover interop calls
- [ ] Compare counts before/after
- [ ] Remove or guard instrumentation for prod builds

**Status:** 🚧 In Progress — HUD overlay + `FlowTime.TopologyCanvas.get/dump/resetHoverDiagnostics` wired up; need before/after comparison + doc updates.

### Task 3.3: UI regression verification
**File(s.):** Playwright/tests scripts (if any)

**Checklist:**
- [ ] Run existing UI smoke tests
- [ ] Manually verify hover behavior on transportation & supply-chain runs
- [ ] Document findings in tracker

**Status:** ⏳ Not Started

---

## Testing & Validation

### Test Case 1: Transportation hover perf
**Status:** ⏳ Not Started

**Steps:**
1. [ ] Load transportation-basic-classes run in UI (class chips enabled)
2. [ ] Capture Chrome Performance trace before changes
3. [ ] Repeat after optimizations, comparing scripting + interop counts

**Expected:**
- Post-fix trace shows ≥50% reduction in hover interop calls and smoother FPS

**Actual:**
- [To be filled during testing]

**Result:** [✅ Pass | ❌ Fail]

### Test Case 2: Supply-chain hover perf
**Status:** ⏳ Not Started

**Steps:**
1. [ ] Load supply-chain-multi-tier-classes run
2. [ ] Hover across edges, monitor inspector responsiveness
3. [ ] Confirm no regressions (stuck hover, delayed inspector)

**Expected:**
- Inspector updates at most once per frame, no stutter

**Actual:**
- [To be filled during testing]

**Result:** [✅ Pass | ❌ Fail]

---

## Issues Encountered

*(To be filled during implementation)*

---

## Final Checklist

### Code Complete
- [ ] All phase tasks complete
- [ ] All tests passing
- [ ] No compilation errors
- [ ] No console warnings
- [ ] Code reviewed (if applicable)

### Documentation
- [ ] Milestone document updated (status → ✅ Complete)
- [ ] ROADMAP.md updated (if required)
- [ ] Release notes entry created (`docs/releases/FT-M-05.06.md`)
- [ ] Performance docs updated with traces

### Quality Gates
- [ ] All unit tests passing
- [ ] UI/Integration tests passing
- [ ] Manual hover perf verification complete
- [ ] Performance acceptable (Chrome traces attached)
- [ ] No regressions detected

## Phase 4: JS Hover Ownership & Timeline UX

### Task 4.1: JS→.NET node hover callback
**Checklist:**
- [ ] RED: Add dev-only instrumentation proving the DOM `@onmouseenter` path is lagging (HUD shows ~1 interop/sec)
- [ ] GREEN: Invoke new `.NET` callback from `topologyCanvas.js` when the hovered node changes (reuse chip hit-test results)
- [ ] REFACTOR: Keep DOM buttons for keyboard focus/accessibility but remove hover work from `HoverNode`

**Tests:**
- [ ] Manual run on transportation model shows tooltips following cursor immediately

**Status:** ⏳ Planned — awaiting start (JS node-hover interop still pending).

### Task 4.2: HUD collapse UX (Done)
**Checklist:**
- [x] Add “Hide” button + collapsed chip
- [x] Store collapsed/expanded state in `localStorage`
- [x] Reset hover stats whenever HUD expands or user clicks Dump

**Status:** ✅ Completed 2025‑12‑11

### Task 4.3: Timeline pointer immediacy (Next)
**Checklist:**
- [ ] Split `OnBinChanged` so pointer/timeline CSS updates happen before heavy recompute
- [ ] Defer `BuildNodeSparklines`/`UpdateActiveMetrics` via background task/`InvokeAsync`
- [ ] Ensure inspector + playback resume once recompute finishes; update run-state persistence accordingly

**Status:** 🆕 Planned — will start after Task 4.1 lands

### Task 4.4: Operational toggle resilience (Bug FT-M-05.06-OP1)
**Checklist:**
- [ ] Detect filter changes and cancel in-flight sparkline/metric recompute
- [ ] Break `BuildNodeSparklines`/`UpdateActiveMetrics` into cancellable async chunks so the UI thread keeps painting
- [ ] Verify toggling “Operational” no longer freezes the browser on large graphs

**Status:** 🆕 Planned — linked to bug FT-M-05.06-OP1; to be addressed after hover/timeline work
