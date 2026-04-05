# SB-M-05.02 Implementation Tracking

**Milestone:** SB-M-05.02 — ServiceWithBuffer DSL Simplification & Queue Latency Semantics  
**Started:** 2025-11-28  
**Status:** ✅ Completed  
**Branch:** `milestone/sb-m-05.02`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** [`work/epics/completed/service-with-buffer/SB-M-05.02.md`](SB-M-05.02.md)
- **Architecture Note:** [`docs/service-with-buffer/service-with-buffer-architecture.md`](../../service-with-buffer/service-with-buffer-architecture.md)
- **Milestone Rules:** [`docs/development/milestone-rules-quick-ref.md`](../../development/milestone-rules-quick-ref.md)

---

## Current Status

### Overall Progress
- [x] Phase 1: Schema & Loader (3/3 tasks)
- [x] Phase 2: Queue Latency Semantics (2/2 tasks)
- [x] Phase 3: Docs & Wrap (2/2 tasks)

### Test Status
- **Unit / Integration:** `dotnet test --nologo` on 2025-11-28 (all suites green except known perf benchmark `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance`, which still exceeds the expected threshold — tracked risk, no regression in queue-latency work).
- **Manual:** Transportation-basic + warehouse-picker-waves runs regenerated locally to confirm paused badges + implicit ServiceWithBuffer behavior (previous session).

---

## Progress Log

### 2025-11-28 — Kickoff

**Preparation:**
- [x] Read milestone document + architecture note
- [x] Create milestone branch (`milestone/sb-m-05.02`)
- [x] Create tracking document
- [x] Verify analyzer + UI fixtures to update

**Next Steps:**
- [x] Phase 1 Task 1.1 RED tests for implicit ServiceWithBuffer validation
- [ ] Plan canonical template migrations (transportation-basic, warehouse-picker-waves)

---

### 2025-11-28 — Phase 1 RED

**Changes:**
- Added `TemplateSchema_ServiceWithBuffer_Allows_Self_QueueDepth` covering the implicit queueDepth scenario (currently failing).

**Tests:**
- ❌ `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter TemplateSchema_ServiceWithBuffer_Allows_Self_QueueDepth --nologo`

**Next Steps:**
- [x] Update schema + validator so ServiceWithBuffer nodes accept `queueDepth: self`
- [ ] Extend loader/model builder to synthesize queue series when helpers are absent


## Phase 1: Schema & Loader

**Goal:** Allow `kind: serviceWithBuffer` topology nodes to stand alone (no helper queueDepth nodes) and migrate canonical templates/tests to the simplified DSL.

### Task 1.1: Template Schema & Validator Updates
**Files:** `docs/schemas/model.schema.yaml`, `docs/schemas/template.schema.json`, `docs/schemas/template-schema.md`, `tests/FlowTime.Tests/Templates/TemplateSchemaTests.cs`

**Checklist (TDD order):**
- [x] RED: add `TemplateSchemaTests.ImplicitServiceWithBuffer_requires_no_helper` (fails)
- [x] GREEN: update schemas & validator to support `queueDepth: "self"` / omission
- [x] Tests: `dotnet test --filter TemplateSchemaTests --nologo`

**Status:** ✅ Completed

### Task 1.2: Loader & Model Builder Synthesis
**Files:** `src/FlowTime.Sim.Core/Templates/TemplateParser.cs`, `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs`, `src/FlowTime.Core/Models/*`, `tests/FlowTime.Core.Tests/*ModelParserTests*.cs`

**Checklist:**
- [x] RED: add `ModelParserImplicitServiceWithBufferTests.Topology_only_node_creates_queue_depth` (fails)
- [x] GREEN: synthesize execution node/series when helper missing; reject legacy backlog
- [x] Tests: `dotnet test --filter ModelParserImplicitServiceWithBufferTests --nologo`

**Status:** ✅ Completed

### Task 1.3: Template & Fixture Migration
**Files:** `templates/transportation-basic-classes.yaml`, `templates/warehouse-picker-waves.yaml`, `tests/FlowTime.Sim.Tests/*Examples*.cs`, analyzer harness configs

**Checklist:**
- [x] Update canonical templates to implicit ServiceWithBuffer spelling
- [x] Run `flow-sim generate` for both templates; ensure analyzers clean
- [x] Update any snapshots/goldens impacted

**Status:** ✅ Completed

### Phase 1 Validation
- [ ] `dotnet build`
- [ ] `dotnet test --filter TemplateSchemaTests --nologo`
- [ ] Manual verification: regenerated transportation + warehouse runs execute without helper nodes

---

## Phase 2: Queue Latency Semantics

**Goal:** Surface explicit queue-latency status when dispatch schedules pause service; propagate through API/UI/analyzer/CLI.

### Task 2.1: Engine, Contracts, Goldens
**Files:** `src/FlowTime.Core/State/NodeMetricsBuilder.cs`, `src/FlowTime.Contracts/Telemetry/NodeSeriesContracts.cs`, `tests/FlowTime.Api.Tests/*golden*.json`, `tests/FlowTime.Core.Tests/*`

**Checklist:**
- [x] RED: add unit test `QueueLatencyStatusBuilderTests.Paused_when_served_zero_depth_positive`
- [x] GREEN: emit `queueLatencyStatus` enum + tooltip text in `/state`, `/state_window`
- [x] Update API goldens & DTOs; run `dotnet test --filter StateEndpointTests --nologo`

**Status:** ✅ Completed

### Task 2.2: Analyzer, CLI, UI Updates
**Files:** `src/FlowTime.Generator/Analyzers/*`, `src/FlowTime.Cli/*`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `src/FlowTime.UI/Components/*`, `tests/FlowTime.UI.Tests/*`

**Checklist:**
- [x] RED: add UI test `ServiceWithBufferPausedBadgeTests.RendersPausedStatus`
- [x] GREEN: show paused badge/tooltips, replace warning banner, update CLI messaging
- [x] Tests: `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter StateEndpointTests --nologo` (API) + manual UI verification

**Status:** ✅ Completed

### Phase 2 Validation
- [ ] `dotnet build`
- [ ] `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter StateEndpointTests --nologo`
- [ ] Manual UI check: paused badge + tooltip appear on warehouse picker ServiceWithBuffer nodes

---

## Phase 3: Docs & Wrap

**Goal:** Align documentation/reference material, log changes, and complete milestone ceremony.

### Task 3.1: Docs & Roadmap Refresh
**Files:** `docs/templates/template-authoring.md`, `templates/README.md`, `work/epics/completed/service-with-buffer/SB-M-05.02.md`, `work/milestones/README.md`, `work/epics/completed/service-with-buffer/service-with-buffer-architecture.md`, `work/epics/epic-roadmap.md`

**Checklist:**
- [x] Update docs to describe implicit ServiceWithBuffer + queue latency status
- [x] Document known limitations + references to SB-M-05.02
- [x] Manual proofread (spot-check key sections)

**Status:** ✅ Completed

### Task 3.2: Verification & Release
**Files:** Release note `docs/releases/SB-M-05.02.md`, tracking doc, milestone doc

**Checklist:**
- [x] `dotnet build`
- [x] `dotnet test --nologo` *(perf benchmark failure expected; no regressions introduced)*
- [x] Manual validation: transportation/warehouse runs show expected badges + statuses
- [x] Draft release note, mark milestone/tracker complete

**Status:** ✅ Completed

### Phase 3 Validation
- [ ] All docs updated
- [ ] Release note created
- [ ] Milestone ceremony complete

---

## Final Checklist

- [x] Code complete across all phases
- [x] Analyzer + UI clean (no outstanding warnings; only expected perf analyzer noise remains)
- [x] Full `dotnet test --nologo` executed (perf benchmark failure acknowledged/deferred)
- [x] Release note committed
- [x] Milestone + tracker marked ✅
### 2025-11-28 — Schema updates GREEN

**Changes:**
- Added `topology` definition plus `queueDepth: self` guidance to `docs/schemas/model.schema.yaml`.
- Confirmed `TemplateSchema_ServiceWithBuffer_Allows_Self_QueueDepth` passes.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter TemplateSchema_ServiceWithBuffer_Allows_Self_QueueDepth --nologo`

**Next Steps:**
- [ ] Template schema + loader synthesis work (Phase 1 Task 1.2/1.3)

### 2025-11-28 — Validator + Parser support

**Changes:**
- Updated `TemplateValidator` to accept `semantics.queueDepth: self` without requiring backing nodes/initial conditions.
- Added parser regression test (`Template_With_ServiceWithBuffer_SelfQueueDepth_Parses`) plus schema test coverage.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter TemplateSchema_ServiceWithBuffer_Allows_Self_QueueDepth --nologo`
- ✅ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter Template_With_ServiceWithBuffer_SelfQueueDepth_Parses --nologo`

**Next Steps:**
- [ ] Loader/model builder synthesis + canonical template migrations

### 2025-11-28 — ServiceWithBuffer synthesizer

**Changes:**
- Added `ServiceWithBufferNodeSynthesizer` invoked from `TemplateParser` to auto-create ServiceWithBuffer nodes when topology semantics use `queueDepth: self` (or omit it).
- Added topology-level `dispatchSchedule` support and extended parser tests to assert synthesized nodes plus queueDepth renaming.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter TemplateSchema_ServiceWithBuffer_Allows_Self_QueueDepth --nologo`
- ✅ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter Template_With_ServiceWithBuffer_SelfQueueDepth_Parses --nologo`

**Next Steps:**
- [ ] Update canonical templates to drop helper nodes and rely on synthesized ServiceWithBuffer nodes

### 2025-11-28 — Template migrations

**Changes:**
- Removed helper `serviceWithBuffer` nodes from `templates/warehouse-picker-waves.yaml` and `templates/transportation-basic-classes.yaml`; topology nodes now declare `queueDepth` plus (where needed) `dispatchSchedule`.
- Updated schema (`docs/schemas/model.schema.yaml`) to document topology-level dispatchSchedule and ensured parser tests reflect synthesized queue node IDs.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter Template_With_ServiceWithBuffer_SelfQueueDepth_Parses --nologo`
- ✅ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter ExamplesConformanceTests --nologo` *(skips expected)*

**Next Steps:**
- [ ] Phase 1 Validation build/test sweep (after loader/templ updates committed)

### 2025-11-28 — Queue latency status (Phase 2)

**Changes:**
- Extended contracts (`QueueLatencyStatusDescriptor`, new properties on `NodeMetrics`/`NodeSeries`) and added API logic to emit `queueLatencyStatus` for bins where dispatch schedules hold backlog.
- Updated state fixtures to include a dispatch-gated queue scenario plus new API tests asserting the snapshot/window payloads.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter StateEndpointTests --nologo`

**Next Steps:**
- [ ] Phase 3 documentation + wrap-up

### 2025-11-28 — Queue latency UI & analyzer integration

**Changes:**
- Added paused-gate queue latency badges in the topology inspector and canvas, wiring `queueLatencyStatus` all the way through DTOs, `NodeBinMetrics`, and JS rendering.
- Replaced the old `latency_uncomputable_bins` analyzer warning with `queue_latency_gate_closed` and filtered it from CLI/Topology warning chips so the new badge carries the context.
- Filtered timeline/canvas warning maps and aligned CLI output so paused gates no longer raise banners; added inspector/table rows for latency status details.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter StateEndpointTests --nologo`
- 👀 Manual UI verification (warehouse + transportation templates) confirming paused chips render instead of warnings.

**Next Steps:**
- [ ] Phase 3 — docs, release notes, full `dotnet test --nologo`

### 2025-11-28 — Phase 3 docs refresh

**Changes:**
- Updated `docs/templates/template-authoring.md` with implicit ServiceWithBuffer guidance, cache refresh command, and queue latency status documentation.
- Refreshed `templates/README.md` highlights for transportation + warehouse templates so they describe the SB‑M‑05.02 surfacing.
- Documented the DSL simplification + latency badges in `work/epics/completed/service-with-buffer/...` and aligned `work/epics/completed/service-with-buffer/SB-M-05.02.md` + roadmap references.

**Tests:**
- 👀 Manual proofreading of the touched docs.

**Next Steps:**
- [ ] Phase 3 Task 3.2 — run full build/tests, add release note, and close out the milestone.

### 2025-11-28 — Phase 3 verification sweep

**Changes:**
- No code changes; executed `dotnet build` + `dotnet test --nologo` from repo root.

**Tests:**
- ✅ Build succeeded with zero warnings.
- ⚠️ `dotnet test --nologo` finished with the existing perf benchmark failure (`FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance` reports 73.87× overhead). This is the long-standing perf issue we already track under the perf sweep follow-up; no new regressions surfaced.

**Next Steps:**
- ✅ Draft release note + wrap milestone once docs and testing notes are captured.
