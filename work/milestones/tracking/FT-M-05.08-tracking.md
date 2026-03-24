# FT-M-05.08 Implementation Tracking

**Milestone:** FT-M-05.08 — ServiceWithBuffer Inspector Consistency and Class Coverage  
**Started:** 2026-01-03  
**Status:** ✅ Complete  
**Branch:** `milestone/ft-m-05.08`  
**Assignee:** Codex  

---

## Quick Links

- **Milestone Document:** `work/milestones/completed/FT-M-05.08-servicewithbuffer-inspector.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Evidence and Gap Inventory (3/3 tasks)
- [x] Phase 2: Engine/API Alignment for Class Coverage (4/4 tasks)
- [x] Phase 3: UI Inspector Alignment (4/4 tasks)
- [x] Phase 4: Documentation and Validation (3/3 tasks)
- [x] Phase 5: Template Narrative + Continuous Buffering (4/4 tasks)

### Test Status
- **dotnet test --nologo:** ✅ pass (perf benchmarks skipped)

---

## Progress Log

### 2026-01-04 - Inspector polish + validation

**Changes:**
- Suppressed full-range highlight rectangles in inspector horizon charts (timeline highlight still renders).
- Expression inspector now uses the shared code-block styling, smaller font, and wraps instead of scrolling.
- Dependency rows restyled for dark mode, tighter padding, and left-aligned text spacing.

**Validation:**
- `dotnet build FlowTime.sln` succeeded.
- `dotnet test --nologo` succeeded (perf tests skipped as expected).
- Manual UI check: chip hover tooltips and ServiceWithBuffer inspector metrics verified.

**Warnings (existing):**
- `tests/FlowTime.UI.Tests/FlowTimeApiClientTests.cs`: CS8604 possible null reference.
- `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`: xUnit2013 collection size assertion.

### 2026-01-03 - Scope update for API-derived ServiceWithBuffer metrics

**Change:** Added API-derivation requirement (queue latency, utilization, service time) plus documentation impact updates to the milestone spec.

**Next:** Add tests + implement derivation rules in `StateQueryService` and align modeling/reference docs.

**Notes:**
- Attempted `dotnet test --filter GetStateWindow_DerivesMetrics_ForServiceWithBuffer` but MSBuild failed to create named pipes (`System.Net.Sockets.SocketException (13): Permission denied`) in the sandbox.
- Retried with `DOTNET_CLI_DISABLE_MSBUILD_SERVER=1` and hit the same named pipe failure.

### 2026-01-04 - Scope update for template narrative + continuous buffering

**Change:** Added Phase 5 to cover template narrative metadata and continuous ServiceWithBuffer modeling (no dispatch schedule) where backlog is intended.

**Next:** Define tests-first checklist and implement schema + metadata propagation.

### 2026-01-04 - Phase 5 narrative + continuous buffering (in progress)

**Changes (tests first):**
- Added RED tests for template narrative propagation in `RunArtifactWriterTests` and `RunManifestReaderTests`.
- Added template schema + metadata propagation fields and updated UI/API DTOs.
- Added narrative guidance to `docs/templates/template-authoring.md`.
- Updated `warehouse-picker-waves` Intake to `serviceWithBuffer` (continuous) and added intake backlog semantics.
- Added narrative lines to catalog templates.

### 2026-01-04 - Phase 5 narrative + continuous buffering (validation)

**Validation:**
- `dotnet test --nologo` succeeded (perf tests skipped as expected).
- Updated golden responses for template metadata narrative coverage.

### 2026-01-03 - Modeling/reference docs aligned for ServiceWithBuffer derivations

**Changes:**
- Added ServiceWithBuffer series requirements to `docs/notes/modeling-queues-and-buffers.md`.
- Linked the guidance from `docs/modeling.md`.
- Documented derivation inputs in `docs/reference/engine-capabilities.md`.

### 2026-01-03 - API derivation tests (ServiceWithBuffer)

**Changes:**
- Ran `dotnet test --filter GetStateWindow_DerivesMetrics_ForServiceWithBuffer` (escalated) — passed.
- Ran `dotnet test --filter GetStateWindow_SkipsServiceMetrics_ForServiceWithBuffer_WhenInputsMissing` (escalated) — passed.

**Warnings:**
- `tests/FlowTime.UI.Tests/FlowTimeApiClientTests.cs`: CS8604 possible null reference.
- `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`: xUnit2013 collection size assertion.
- `src/FlowTime.Sim.Core/Services/ProvenanceEmbedder.cs`: CS8604 possible null reference.
- `src/FlowTime.Generator/Orchestration/RunOrchestrationService.cs`: CS0105 duplicate using directive.

### 2026-01-03 - Template updates for ServiceWithBuffer metrics

**Changes:**
- Added capacity + service time series semantics for ServiceWithBuffer nodes in `templates/transportation-basic.yaml`.
- Added capacity + service time series semantics for ServiceWithBuffer nodes in `templates/transportation-basic-classes.yaml`.
- Bumped template versions to reflect the updated semantics.
 - Added service time series semantics for ServiceWithBuffer nodes in `templates/warehouse-picker-waves.yaml` and bumped template version.

### 2026-01-03 - Phase 1 evidence capture (transportation-basic-classes)

**Run audited:** `data/runs/run_20251214T151352Z_479f8f01` (classCoverage: full in `run.json`)

**/graph semantics (model.yaml):**
- CentralHub (service): `arrivals_hub@DEFAULT`, `served_hub@DEFAULT`, `unmet_hub@DEFAULT`, `cap_hub@DEFAULT`
- HubQueue (serviceWithBuffer): `hub_queue_demand@DEFAULT`, `hub_dispatch@DEFAULT`, `hub_queue_attrition@DEFAULT`, `hub_queue_depth@DEFAULT`
- AirportDispatchQueue (serviceWithBuffer): `airport_dispatch_queue_demand@DEFAULT`, `airport_dispatch_queue_served@DEFAULT`, `airport_dispatch_queue_attrition@DEFAULT`, `airport_dispatch_queue_depth@DEFAULT`

**/state_window series evidence (series files):**
- CentralHub: arrivals/served/errors each have class series for Airport/Downtown/Industrial (`arrivals_hub@...@Airport`, `served_hub@...@Downtown`, `unmet_hub@...@Industrial`).
- HubQueue: demand/dispatch/attrition/depth each have Airport/Downtown/Industrial class series (`hub_queue_*@...@{class}`).
- AirportDispatchQueue: demand/served/depth have Airport class series only; `airport_dispatch_queue_attrition` only has DEFAULT.
- Downtown/Industrial dispatch queues mirror the same single-class pattern (class series only for their own class; attrition DEFAULT only).

**Initial gap notes:**
- ServiceWithBuffer inspector currently omits queue metrics (code gap).
- Downstream dispatch queues are single-class by model design; if UI shows zero class chips, that is a code gap, not a missing series.

### 2026-01-03 - Phase 2 test kickoff

**Changes:**
- Added RED unit test `ServiceWithBuffer_UsesRouterOutputsForClassSeries` to validate router outputs propagate into ServiceWithBuffer class contributions.
 - Attempted `dotnet test --filter ServiceWithBuffer_UsesRouterOutputsForClassSeries` (with and without `--disable-build-servers`); MSBuild failed with `System.Net.Sockets.SocketException (13): Permission denied` creating NamedPipeServerStream.

### 2026-01-03 - Phase 2 class propagation validation

**Changes:**
- Re-ran `dotnet test --filter ServiceWithBuffer_UsesRouterOutputsForClassSeries` with escalated permissions (required for MSBuild named pipes); test passed.
- Added `GetStateWindow_ReturnsByClassSeries_ForQueueNode` plus fixture run `run_state_classes_queue` to assert ByClass for queue/serviceWithBuffer telemetry nodes.
- Ran `dotnet test --filter GetStateWindow_ReturnsByClassSeries_ForQueueNode` (escalated) and it passed.
- Added `TransportationQueuesExposeClassSeries` integration test over `transportation-basic-classes` template; `dotnet test --filter TransportationQueuesExposeClassSeries` passed.

**Warnings:**
- `FlowTime.Sim.Core/Services/ProvenanceEmbedder.cs`: CS8604 possible null reference.
- `FlowTime.Generator/Orchestration/RunOrchestrationService.cs`: CS0105 duplicate using directive.

### 2026-01-03 - Phase 3 inspector coverage

**Changes:**
- Added serviceWithBuffer inspector tests (queue metrics, alias handling, retry gating) and a class-chip regression in UI tests.
- Updated inspector metric composition to treat serviceWithBuffer as queue-like plus service metrics, and gate retry metrics by presence.
- Ran `dotnet test --filter BuildInspectorMetrics_ServiceWithBuffer` and `--filter ClassContributionsExposePerClassMetrics_ForServiceWithBuffer` (escalated); tests passed.

**Warnings:**
- `FlowTime.UI/Components/Templates/SimulationResults.razor`: CS8602 possible null reference.
- `FlowTime.UI/Pages/ArtifactDetail.razor`: CS1998 async method lacks await.
- `FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`: CS8604 possible null reference.
- `FlowTime.UI/Pages/TimeTravel/Topology.razor`: CS1998 async method lacks await.
- `FlowTime.UI/Components/Templates/SimulationResults.razor`: CS0414 unused field.
- `FlowTime.UI/Pages/TimeTravel/RunOrchestration.razor`: MUD0002 illegal attribute.
- `tests/FlowTime.UI.Tests/FlowTimeApiClientTests.cs`: CS8604 possible null reference.
- `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`: xUnit2013 collection size assertion.

---

## Phase 1: Evidence and Gap Inventory

**Goal:** Separate model gaps from code gaps with `/graph` + `/state_window` series audits.

### Task 1.1: Capture `/graph` and `/state_window` series for target nodes
**Checklist (TDD Order - Tests FIRST):**
- [x] Define validation checklist for series capture (RED)
- [x] Capture series keys for CentralHub, HubQueue, AirportDispatchQueue (GREEN)
- [x] Record results in tracking log (GREEN)

**Status:** ✅ Complete

### Task 1.2: Build metric gap matrix
**Checklist (TDD Order - Tests FIRST):**
- [x] Define expected metric contract for service vs ServiceWithBuffer (RED)
- [x] Map actual vs expected series in matrix (GREEN)
- [x] Tag gaps as model vs code (GREEN)

**Status:** ✅ Complete

### Task 1.3: Document gap classifications
**Checklist (TDD Order - Tests FIRST):**
- [x] Add gap summary table to tracking log (RED)
- [x] Link to captured artifacts (GREEN)

**Status:** ✅ Complete

### Phase 1 Validation
- [x] `/graph` + `/state_window` keys captured for target nodes
- [x] Gap matrix completed with model/code classification

### Gap Matrix (Initial)

**Artifacts referenced:** `data/runs/run_20251214T151352Z_479f8f01/model/model.yaml`, `data/runs/run_20251214T151352Z_479f8f01/series/*`

**CentralHub (service)**
- **Series present:** arrivals_hub, served_hub, unmet_hub, cap_hub, hub_processing_time_ms_sum (class series for Airport/Downtown/Industrial).
- **Gaps:** retry metrics (attempts/failures/retryEcho) not present.
- **Classification:** Model gap (template does not define retry series for CentralHub).

**HubQueue (serviceWithBuffer)**
- **Series present:** hub_queue_demand, hub_dispatch, hub_queue_attrition, hub_queue_depth (class series for Airport/Downtown/Industrial).
- **Gaps:** inspector omits queue depth/latency for ServiceWithBuffer despite queue series existing.
- **Classification:** Code gap (UI inspector).
- **Additional:** serviceTimeMs/flowLatency require processingTimeMsSum/servedCount (not in template).
- **Classification:** Model gap (missing series).

**AirportDispatchQueue (serviceWithBuffer)**
- **Series present:** airport_dispatch_queue_demand/served/depth (Airport class only), attrition (DEFAULT only).
- **Gaps:** class coverage is single-class by design; missing per-class errors (attrition) series.
- **Classification:** Model gap (template only provides DEFAULT attrition; router routes a single class per queue).
- **Note:** If UI shows zero class chips for this node, that is a code gap (ByClass exists for Airport).

---

## Phase 2: Engine/API Alignment for Class Coverage

**Goal:** Ensure class data propagates to ServiceWithBuffer nodes downstream of routers.

### Task 2.1: Verify class propagation in `ClassContributionBuilder`
**Checklist (TDD Order - Tests FIRST):**
- [x] Add unit test: `ServiceWithBuffer_UsesRouterOutputsForClassSeries` (RED)
- [x] Validate class propagation for router targets (GREEN)
- [x] Update implementation if needed (GREEN)

**Status:** ✅ Complete

### Task 2.2: Ensure `/state_window` exposes ByClass for ServiceWithBuffer nodes
**Checklist (TDD Order - Tests FIRST):**
- [x] Add integration test: `GetStateWindow_ReturnsByClassSeries_ForQueueNode` (RED)
- [x] Update mapping in `StateQueryService` if needed (GREEN)

**Status:** ✅ Complete

### Task 2.3: Regression test for classed template coverage
**Checklist (TDD Order - Tests FIRST):**
- [x] Add test fixture for `transportation-basic-classes` downstream queues (RED)
- [x] Confirm class chips appear in UI data output (GREEN)

**Status:** ✅ Complete

### Task 2.4: API derivation for ServiceWithBuffer metrics
**Checklist (TDD Order - Tests FIRST):**
- [x] Add API test: queue latency series derived for ServiceWithBuffer when queue depth + served exist (RED)
- [x] Add API test: utilization/service time derived for ServiceWithBuffer when capacity/processing time inputs exist (RED)
- [x] Update `StateQueryService` derivation rules (GREEN)
- [x] Add guard tests for missing inputs (No data remains) (GREEN)

**Status:** ✅ Complete

### Phase 2 Validation
- [x] Unit + integration tests passing for class propagation
- [x] `/state_window` includes ByClass for ServiceWithBuffer nodes where class data exists

---

## Phase 3: UI Inspector Alignment

**Goal:** Align ServiceWithBuffer inspector metrics with the documented contract.

### Task 3.1: Queue metrics for ServiceWithBuffer
**Checklist (TDD Order - Tests FIRST):**
- [x] Add UI test: `BuildInspectorMetrics_ServiceWithBuffer_IncludesQueueMetrics` (RED)
- [x] Update inspector metric blocks to include queue depth/latency (GREEN)

**Status:** ✅ Complete

### Task 3.2: Alias handling consistency
**Checklist (TDD Order - Tests FIRST):**
- [x] Add unit test: `BuildInspectorMetrics_ServiceWithBuffer_UsesQueueAlias` (RED)
- [x] Normalize `queue` vs `queueDepth` mapping (GREEN)

**Status:** ✅ Complete

### Task 3.3: Retry metrics gating
**Checklist (TDD Order - Tests FIRST):**
- [x] Add UI test: `BuildInspectorMetrics_ServiceWithBuffer_ExcludesRetryMetricsWhenAbsent` (RED)
- [x] Ensure missing retry series show placeholders (GREEN)

**Status:** ✅ Complete

### Task 3.4: Class chips visibility in inspector
**Checklist (TDD Order - Tests FIRST):**
- [x] Add UI test: `ClassContributionsExposePerClassMetrics_ForServiceWithBuffer` (RED)
- [x] Ensure class chips render when ByClass data exists (GREEN)

**Status:** ✅ Complete

### Phase 3 Validation
- [x] Inspector metric blocks match service + ServiceWithBuffer contract
- [x] Class chips visible when class data exists

---

## Phase 4: Documentation and Validation

**Goal:** Document requirements and confirm validation steps.

### Task 4.1: Authoring guidance update
**Checklist (TDD Order - Tests FIRST):**
- [x] Update `docs/templates/template-authoring.md` with ServiceWithBuffer inspector requirements (GREEN)

**Status:** ✅ Complete

### Task 4.2: Validation checklist
**Checklist (TDD Order - Tests FIRST):**
- [x] Add validation checklist to tracking log (GREEN)

**Status:** ✅ Complete

### Task 4.3: Modeling + reference docs alignment
**Checklist (TDD Order - Tests FIRST):**
- [x] Update `docs/modeling.md` for required ServiceWithBuffer series inputs (GREEN)
- [x] Update `docs/reference/*` to reflect API-derivation rules (GREEN)

**Status:** ✅ Complete

### Validation Checklist
- [x] Run `dotnet build`
- [x] Run `dotnet test --nologo`
- [x] Manual UI check: ServiceWithBuffer inspector shows queue depth + latency + service metrics.
- [x] Manual UI check: class chips show for HubQueue + dispatch queues in `transportation-basic-classes`.

### Phase 4 Validation
- [x] Documentation updated
- [x] Validation checklist complete

---

## Phase 5: Template Narrative + Continuous Buffering

**Goal:** Add narrative metadata and model continuous buffering with ServiceWithBuffer where intended.

### Task 5.1: Template narrative schema + model propagation
**Checklist (TDD Order - Tests FIRST):**
- [x] Add RED tests for template metadata narrative propagation (template -> metadata.json -> RunManifest).
- [x] Update template schema + Sim metadata models (GREEN).
- [x] Update RunArtifactWriter + RunManifestReader + API DTOs (GREEN).

**Status:** ✅ Complete

### Task 5.2: Template authoring docs update
**Checklist (TDD Order - Tests FIRST):**
- [x] Add narrative guidance section and example (RED).
- [x] Update docs and verify schema/authoring guidance references (GREEN).

**Status:** ✅ Complete

### Task 5.3: Continuous buffering template audit + upgrades
**Checklist (TDD Order - Tests FIRST):**
- [x] Identify templates where backlog should persist without gated releases (RED).
- [x] Convert service -> serviceWithBuffer (no dispatchSchedule) where appropriate (GREEN).
- [x] Bump template versions and validate schema (GREEN).

**Status:** ✅ Complete

### Task 5.4: Validation + docs sync
**Checklist (TDD Order - Tests FIRST):**
- [x] Update modeling/telemetry docs if required by template changes (RED).
- [x] Verify template changes still pass existing tests (GREEN).

**Status:** ✅ Complete

---

## Final Checklist

- [x] All phase tasks complete
- [x] `dotnet build` passes
- [x] `dotnet test --nologo` passes
- [x] Milestone document updated (status -> ✅ Complete)
- [x] Release notes added
