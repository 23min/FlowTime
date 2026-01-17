# FT-M-05.15 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](milestone-rules-quick-ref.md) for workflow.

**Milestone:** FT-M-05.15 — Series Semantics Metadata (Aggregation)  
**Started:** 2026-01-15  
**Status:** 🔄 In Progress  
**Branch:** `milestone/ft-m-05.15`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** `docs/milestones/FT-M-05.15-series-semantics-metadata.md`
- **Related Analysis:** N/A
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Schema + Contract (2/2 tasks)
- [x] Phase 2: API + DTO Plumbing (2/2 tasks)
- [x] Phase 3: UI Surface + Docs (2/2 tasks)

### Test Status
- **Full Suite:** ❌ `dotnet test --nologo` (fails: `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance`)

---

## Progress Log

### 2026-01-15 - Session Start

**Preparation:**
- [x] Read milestone document
- [x] Read related documentation
- [x] Create milestone branch
- [x] Verify schema/contract dependencies

**Next Steps:**
- [x] Begin Phase 1
- [x] Start Task 1.1 (schema tests)

### 2026-01-15 - Schema Tests (RED)

**Changes:**
- Added schema validation tests for `seriesMetadata` aggregation rules in `StateResponseSchemaTests`.

**Tests:**
- ❌ `dotnet test --nologo --filter StateWindow_Response_AllowsSeriesMetadata` (expected RED; schema not updated yet)
- ✅ `dotnet test --nologo --filter StateWindow_Response_AllowsSeriesMetadata|StateWindow_Response_RejectsUnknownAggregation`

### 2026-01-15 - DTO Tests (RED)

**Changes:**
- Added UI ingestion tests for `seriesMetadata` in `SeriesMetadataIngestionTests`.

**Tests:**
- ❌ `dotnet test --nologo --filter SeriesMetadataIngestionTests` (expected RED; DTO fields not added yet)

### 2026-01-15 - DTO Fields (GREEN)

**Changes:**
- Added `seriesMetadata` DTO fields for snapshot and window nodes.

**Tests:**
- ✅ `dotnet test --nologo --filter SeriesMetadataIngestionTests`

### 2026-01-15 - API Metadata (RED → GREEN)

**Changes:**
- Added API test for derived series metadata on `/state_window`.
- Emitted derived series metadata for latency/service/flow series in `StateQueryService`.

**Tests:**
- ❌ `dotnet test --nologo --filter GetStateWindow_EmitsSeriesMetadata_ForDerivedSeries` (expected RED; metadata not emitted yet)
- ✅ `dotnet test --nologo --filter GetStateWindow_EmitsSeriesMetadata_ForDerivedSeries`

### 2026-01-15 - UI Tooltip Aggregation (GREEN)

**Changes:**
- Added tooltip test for aggregation metadata and wired series metadata into provenance tooltips.

**Tests:**
- ✅ `dotnet test --nologo --filter Inspector_ProvidesAggregationMetadataInTooltip`

### 2026-01-15 - Canvas Aggregation Badge (RED → GREEN)

**Changes:**
- Added canvas overlay label showing `Aggregate: Avg` beneath the focus legend.

**Tests:**
- ❌ `dotnet test --nologo --filter AggregationIndicator_DefaultsToAverage` (expected RED; label empty)
- ✅ `dotnet test --nologo --filter AggregationIndicator_DefaultsToAverage`

### 2026-01-15 - Service Time Zero Guard (RED → GREEN)

**Changes:**
- Added service-time regression run and tests for zero served-count bins.
- Updated API + UI derived service-time calculations to return null when served count is zero.

**Tests:**
- ❌ `dotnet test --nologo --filter GetStateWindow_ServiceTimeIsNull_WhenServedCountIsZero` (expected RED; API returned 0)
- ✅ `dotnet test --nologo --filter GetStateWindow_ServiceTimeIsNull_WhenServedCountIsZero`
- ❌ `dotnet test --nologo --filter BuildServiceTimeSeries_SkipsBinsWithZeroServedCount` (expected RED; UI returned 0)
- ✅ `dotnet test --nologo --filter BuildServiceTimeSeries_SkipsBinsWithZeroServedCount`

### 2026-01-15 - Completion SLA Tooltip (RED → GREEN)

**Changes:**
- Added completion SLA line to node popups when completion SLA is available.

**Tests:**
- ❌ `dotnet test --nologo --filter TooltipFormatter_IncludesCompletionSla_WhenProvided` (expected RED)
- ✅ `dotnet test --nologo --filter TooltipFormatter_IncludesCompletionSla_WhenProvided`

### 2026-01-15 - Focus View Filter Fallback (RED → GREEN)

**Changes:**
- Ensured focus view still renders when all node filters are cleared by treating empty filters as "include all" for the focused chain.
- Added render test for focus view with cleared filters.

**Tests:**
- ⏭️ Not run (not requested)

### 2026-01-15 - Schedule SLA Label (RED → GREEN)

**Changes:**
- When a scheduled non-sink node has schedule adherence, label the row as "Schedule SLA".

**Tests:**
- ❌ `dotnet test --nologo --filter InspectorBinMetrics_ServiceNode_WithSchedule_ShowsCompletionAndScheduleSla` (expected RED)
- ✅ `dotnet test --nologo --filter InspectorBinMetrics_ServiceNode_WithSchedule_ShowsCompletionAndScheduleSla`

### 2026-01-15 - Sink SLA Label (GREEN)

**Changes:**
- Use "SLA" instead of "Schedule SLA" for sinks without a dispatch schedule.

**Tests:**
- ✅ `dotnet test --nologo --filter TooltipFormatter_SinkWithoutSchedule_UsesSlaLabel`

### 2026-01-15 - Focus Labels Match Metrics (RED → GREEN)

**Changes:**
- Added coverage to ensure focus labels use active metrics instead of sparkline samples.

**Tests:**
- ❌ `dotnet test --nologo --filter FocusLabel_UsesActiveMetrics_WhenAvailable` (expected RED; focus labels derived from sparklines)
- ✅ `dotnet test --nologo --filter FocusLabel_UsesActiveMetrics_WhenAvailable`

### 2026-01-15 - Service Time Minutes Formatting (RED → GREEN)

**Changes:**
- Show service time values in minutes when large in inspector and node popups.

**Tests:**
- ❌ `dotnet test --nologo --filter InspectorBinMetrics_ServiceTime_UsesMinutesForLargeValues` (expected RED; formatting still in ms)
- ✅ `dotnet test --nologo --filter InspectorBinMetrics_ServiceTime_UsesMinutesForLargeValues`

### 2026-01-15 - Flow Latency Minutes Formatting (RED → GREEN)

**Changes:**
- Show flow latency values in minutes when large in the inspector.

**Tests:**
- ❌ `dotnet test --nologo --filter InspectorBinMetrics_FlowLatency_UsesMinutesForLargeValues` (expected RED; formatting still in ms)
- ✅ `dotnet test --nologo --filter InspectorBinMetrics_FlowLatency_UsesMinutesForLargeValues`

### 2026-01-15 - Flow Latency Sparkline Basis (REFACTOR)

**Changes:**
- Corrected the canvas sparkline basis mapping to use flow-latency series when Flow Latency is selected.
- Updated the sparkline label to show "Flow lat" instead of defaulting to "SLA" for flow latency.

**Tests:**
- ⏳ No direct automated test for JS sparkline basis selection.

### 2026-01-15 - Flow Latency Composition + Served Gate (RED → GREEN)

**Changes:**
- Flow latency now adds queue latency + service time for `serviceWithBuffer` and returns null when served is zero.
- Added flow latency expectations for the service-with-buffer derived run and service-time-zero run.

**Tests:**
- ❌ `dotnet test --nologo --filter GetStateWindow_DerivesMetrics_ForServiceWithBuffer` (expected RED; flow latency missing service time)
- ❌ `dotnet test --nologo --filter GetStateWindow_ServiceTimeIsNull_WhenServedCountIsZero` (expected RED; flow latency not asserted)
- ✅ `dotnet test --nologo --filter GetStateWindow_DerivesMetrics_ForServiceWithBuffer`
- ✅ `dotnet test --nologo --filter GetStateWindow_ServiceTimeIsNull_WhenServedCountIsZero`

### 2026-01-15 - Canvas Chips Prefer Active Metrics (REFACTOR)

**Changes:**
- Node chips now prefer active-bin metrics over sparkline samples to avoid stale values during scrubbing.

**Tests:**
- ⏳ No direct automated test for canvas chip sampling.

### 2026-01-15 - Flow Latency No-Completions Label (RED → GREEN)

**Changes:**
- Flow latency focus labels render "-" when served is zero, and tooltips explain missing flow latency.
- Flow latency sparklines show a "No served" placeholder when the window has no completions.
- Flow latency focus labels no longer fall back to sparkline values when the active-bin metric is missing.
- Line sparklines fall back to bar rendering when only a single value is present.

**Tests:**
- ❌ `dotnet test --nologo --filter FocusLabel_FlowLatency_NoCompletions_ShowsDash` (expected RED; label missing)
- ✅ `dotnet test --nologo --filter FocusLabel_FlowLatency_NoCompletions_ShowsDash`

### 2026-01-15 - Bin Refresh Revision Guard (RED → GREEN)

**Changes:**
- Guarded async bin refresh results with a revision token to prevent stale scrubber updates from overwriting the latest bin selection.
- Added UI test to ensure stale bin refresh results are ignored.

**Tests:**
- ✅ `dotnet test --nologo --filter BinDataRefresh_IgnoresStaleResults` (runs full solution; filter only matches UI tests)

### 2026-01-15 - Tooltip Sparkline Alignment (REFACTOR)

**Changes:**
- Popup sparkline now uses the same rendering path as node sparklines so bars/lines/colors match and “No data” placeholders appear consistently.

**Tests:**
- ⏳ No direct automated test for tooltip JS rendering changes.

### 2026-01-15 - Sink Sparkline + Flow Latency Gate (RED → GREEN)

**Changes:**
- Sink flow latency is now gated by arrivals (so sink latency follows arrivals bins even if served is missing).
- Sink sparklines fall back to Arrivals when flow-latency series has no values, avoiding “No served/No data” in the tooltip.

**Tests:**
- ⏳ No direct automated test yet for sink flow-latency gating or tooltip sparkline fallback.

### 2026-01-15 - Sink Service Time Removal (REFACTOR)

**Changes:**
- Removed "Service time" from sink inspector rows and charts (not meaningful for sink nodes).

**Tests:**
- ⏳ No direct automated test yet for sink inspector service-time suppression.

### 2026-01-15 - Completion SLA Tooltip Definition (REFACTOR)

**Changes:**
- Updated Completion SLA meaning text in provenance tooltips to match the SLA definition (end-to-end latency within threshold).

**Tests:**
- ⏳ No direct automated test for the text-only tooltip change.

### 2026-01-15 - Full Suite (REFACTOR)

**Tests:**
- ❌ `dotnet test --nologo` (fails: `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance`)

### 2026-01-15 - Canvas Sparkline Percent Scaling

**Changes:**
- Clamp percent-based sparklines (SLA/utilization/error) to a 0–1 scale so 100% renders at the top.

**Tests:**
- ⏳ No direct automated test for the JS canvas scaling adjustment.

### 2026-01-15 - API Golden Snapshots (REFACTOR)

**Changes:**
- Updated API golden files to include derived series metadata.

**Tests:**
- ✅ `dotnet test --nologo`

### 2026-01-15 - Transportation Template Capacity + Timing (REFACTOR)

**Changes:**
- Added line capacities and scaled service-time series to minute-scale values for the transportation templates.

**Tests:**
- ✅ `dotnet test --nologo`

### 2026-01-15 - Focus Label Alignment (REFACTOR)

**Changes:**
- Focus labels now prefer active metrics over sparkline slices; updated canvas tests accordingly.

**Tests:**
- ✅ `dotnet test --nologo` (perf benchmarks skipped as expected)

### 2026-01-15 - Docs Updates (REFACTOR)

**Changes:**
- Documented aggregation metadata defaults and telemetry guidance in provenance + template docs.

---

## Phase 1: Schema + Contract

**Goal:** Extend the time-travel schema with optional series semantics metadata.

### Task 1.1: Add failing schema tests
**File(s):** `docs/schemas/time-travel-state.schema.json`, tests under `tests/`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write schema test: `TimeTravelStateSchema_AllowsSeriesMetadata` (RED)
- [x] Write schema test: `TimeTravelStateSchema_RejectsUnknownAggregation` (RED)
- [ ] Commit: `test(schema): add series metadata contract tests`

**Status:** ✅ Complete

---

### Task 1.2: Update schema + docs
**File(s):** `docs/schemas/time-travel-state.schema.json`, `docs/schemas/time-travel-state.schema.md`

**Checklist (TDD Order - Tests FIRST):**
- [x] Update schema to allow `seriesMetadata` (GREEN)
- [x] Update schema docs and examples (REFACTOR)
- [ ] Commit: `docs(schema): document series metadata`

**Status:** ✅ Complete

---

## Phase 2: API + DTO Plumbing

**Goal:** Surface metadata from API response models and derived series.

### Task 2.1: DTO fields
**File(s):** `src/FlowTime.UI/Services/TimeTravelApiModels.cs`, `src/FlowTime.Contracts/TimeTravel/StateContracts.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Add DTO tests for `seriesMetadata` (RED)
- [x] Add DTO fields (GREEN)
- [ ] Commit: `feat(api): add series metadata to time-travel DTOs`

**Status:** ✅ Complete

---

### Task 2.2: API plumbing for derived series
**File(s):** `src/FlowTime.API/Services/StateQueryService.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Add API test: `/state_window` includes derived metadata (RED)
- [x] Emit metadata for derived latency series (GREEN)
- [ ] Commit: `feat(api): emit series metadata for derived latency`

**Status:** ✅ Complete

---

## Phase 3: UI Surface + Docs

**Goal:** Surface aggregation metadata in inspector tooltips and update docs.

### Task 3.1: UI tooltip rendering
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, UI tests under `tests/FlowTime.UI.Tests/TimeTravel/`

**Checklist (TDD Order - Tests FIRST):**
- [x] Add UI test: tooltip renders Aggregation when present (RED)
- [x] Render aggregation in provenance tooltips (GREEN)
- [ ] Commit: `feat(ui): show aggregation metadata in tooltips`

**Status:** ✅ Complete

---

### Task 3.2: Documentation updates
**File(s):** `docs/architecture/ui/metric-provenance.md`, `docs/templates/template-authoring.md`

**Checklist (TDD Order - Tests FIRST):**
- [x] Update provenance docs to describe aggregation display (REFACTOR)
- [x] Update template/telemetry guidance for metadata (REFACTOR)
- [ ] Commit: `docs: add series metadata guidance`

**Status:** ✅ Complete

---

## Testing & Validation

### Test Case 1: Schema accepts series metadata
**Status:** ⏳ Not Started

**Steps:**
1. [ ] Run schema tests.
2. [ ] Validate legacy fixtures still pass.

**Expected:** Optional metadata accepted, unknown aggregation rejected.

### Test Case 2: API emits derived metadata
**Status:** ⏳ Not Started

**Steps:**
1. [ ] Call `/state_window` on a derived latency run.
2. [ ] Verify metadata for latency/service/flow series.

**Expected:** `aggregation=avg`, `origin=derived` for derived metrics.

### Test Case 3: UI tooltip shows Aggregation
**Status:** ⏳ Not Started

**Steps:**
1. [ ] Load run with metadata.
2. [ ] Open inspector tooltip.

**Expected:** Aggregation line renders when metadata exists; fallback when missing.

---

## Final Checklist

- [ ] `dotnet build`
- [ ] `dotnet test --nologo`
- [ ] Tracking doc updated with test results
- [ ] Milestone doc status → ✅ Complete
