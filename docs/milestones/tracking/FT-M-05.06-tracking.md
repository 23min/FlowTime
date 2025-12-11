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
- [ ] Phase 1: Input Guardrails & Throttling (canvas gating + RAF coded, tests pending)
- [ ] Phase 2: JS/Interop Optimizations (edge dedupe + inspector debounce coded; tests pending)
- [ ] Phase 3: Profiling & Validation (0/3 tasks)

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

**Status:** 🚧 In Progress — bounding-box gating implemented in JS; automated test + .razor helper refactor still pending.

### Task 1.2: `requestAnimationFrame` throttling
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`

**Checklist:**
- [ ] RED: Record perf counter showing multiple hover calls per frame
- [ ] GREEN: Wrap hover updates in `requestAnimationFrame`
- [ ] REFACTOR: Ensure cancellation/reset logic handles canvas dispose

**Tests:**
- [ ] Manual perf trace shows ≤1 hover update per frame

**Status:** 🚧 In Progress — RAF queue + cancellation code committed; perf trace + disposal guards outstanding.

### Task 1.3: Skip redundant redraws
**File(s):** `TopologyCanvas.razor(.cs)`, JS hover helper

**Checklist:**
- [ ] Detect when computed hover ID matches previous value
- [ ] Bypass interop/invalidate paths when nothing changed
- [ ] Confirm inspector hover highlights still update correctly

**Tests:**
- [ ] UI smoke verifying hover chips still respond when moving between edges

**Status:** 🚧 In Progress — JS + Blazor handlers now short-circuit duplicate IDs; need instrumentation counter + tests.

---

## Phase 2: JS/Interop Optimizations

**Goal:** Reduce .NET interop churn by deduplicating events, caching hit-tests, and batching inspector updates.

### Task 2.1: De-duplicate `OnEdgeHoverChanged`
**File(s):** `TopologyCanvas.razor(.cs)`, `topologyCanvas.js`

**Checklist:**
- [ ] Track last hovered edge ID in JS and .NET
- [ ] Avoid null→null and same-ID calls into .NET
- [ ] Add logging counter (behind debug flag) to confirm reductions

**Status:** 🚧 In Progress — hover cache in JS reduces redundant hit-tests; threshold tuning + perf verification outstanding.

### Task 2.2: Hit-test caching
**File(s):** `topologyCanvas.js`

**Checklist:**
- [ ] Cache last pointer position and bounding boxes
- [ ] Reuse cached hit test results when pointer movement < threshold
- [ ] Expose knob for sensitivity if needed

**Status:** 🚧 In Progress — Canvas event handler now debounced via CTS; need to prove inspector stays in sync + add coverage.

### Task 2.3: Debounce inspector updates
**File(s):** `TopologyInspector.razor`, state services

**Checklist:**
- [ ] Introduce debounce or RAF batching for inspector state updates
- [ ] Ensure un-hover still clears state promptly
- [ ] Validate no stale selections remain when switching nodes quickly

**Status:** 🚧 In Progress — instrumentation hooks + `FlowTime.TopologyCanvas.getHoverDiagnostics` added; need to capture before/after counts + document results.

---

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
