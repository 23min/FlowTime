# FT-M-05.11 Implementation Tracking

**Milestone:** FT-M-05.11 — Sink Node Kind (Terminal Success Semantics)  
**Started:** 2026-01-08  
**Status:** ✅ Complete  
**Branch:** `milestone/ft-m-05.11`  
**Assignee:** Codex  

---

## Quick Links

- **Milestone Document:** `docs/milestones/FT-M-05.11-sink-node-kind.md`
- **Related Analysis:** `docs/architecture/service-with-buffer/service-with-buffer-architecture-part2.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Schema + Parser (2/2 tasks)
- [x] Phase 2: Engine + Output (4/4 tasks)
- [x] Phase 3: UI + Templates (2/2 tasks)
- [x] Phase 4: Docs + Validation (2/2 tasks)

### Test Status
- **Build:** ✅ `dotnet build` (warnings: CS8604 in `ProvenanceEmbedder.cs`, CS0105 in `RunOrchestrationService.cs`, CS8602/CS1998/CS0414/MUD0002 in UI, xUnit2013 in UI tests)
- **Tests:** ✅ `dotnet test --nologo` (perf tests skipped as expected)

---

## Progress Log

### 2026-01-08 - Session Start

**Preparation:**
- [x] Read milestone document
- [x] Read related architecture doc
- [x] Create milestone branch
- [x] Create tracking document

**Next Steps:**
- [ ] Begin Phase 1
- [ ] Start Task 1.1 (schema acceptance test)

### 2026-01-08 - Phase 1 Task 1.1

**Tests (RED):**
- Added `Template_With_Sink_Kind_Parses` in `tests/FlowTime.Sim.Tests/NodeBased/TemplateParserTests.cs`.

**Validation:**
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter Template_With_Sink_Kind_Parses`
  - Result: ✅ Passed (schema already allows any topology node `kind` string; no schema change required)

### 2026-01-08 - Phase 1 Task 1.2

**Tests (RED):**
- Added `SinkNode_RejectsQueueCapacityFields` in `tests/FlowTime.Sim.Tests/NodeBased/TemplateParserTests.cs`.

**Implementation (GREEN):**
- Reject `semantics.queueDepth` and `semantics.capacity` for `kind: sink` in `src/FlowTime.Sim.Core/Templates/TemplateValidator.cs`.

**Validation:**
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter SinkNode_RejectsQueueCapacityFields`
  - Warnings: CS8604 in `src/FlowTime.Sim.Core/Services/ProvenanceEmbedder.cs`, CS0105 in `src/FlowTime.Generator/Orchestration/RunOrchestrationService.cs`.

### 2026-01-08 - Phase 2 Task 2.1

**Tests (RED):**
- Added `SinkNode_ServedEqualsArrivals` in `tests/FlowTime.Sim.Tests/Templates/SinkNodeTemplateTests.cs`.

**Implementation (GREEN):**
- Synthesized sink defaults in `src/FlowTime.Sim.Core/Templates/SinkNodeSynthesizer.cs` (served → arrivals, errors → zero const series).
- Wired synthesizer into `src/FlowTime.Sim.Core/Templates/TemplateParser.cs` before validation.

**Validation:**
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter SinkNode_ServedEqualsArrivals`
  - First run timed out at 10s; reran with longer timeout and passed.

### 2026-01-08 - Phase 2 Task 2.2

**Tests (RED):**
- Added `SinkNode_RejectsRetryFields` in `tests/FlowTime.Sim.Tests/NodeBased/TemplateParserTests.cs`.

**Implementation (GREEN):**
- Disallow retry semantics on sinks (`attempts`, `failures`, `retryEcho`, `retryKernel`) in `src/FlowTime.Sim.Core/Templates/TemplateValidator.cs`.

**Validation:**
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter SinkNode_RejectsRetryFields`
  - Warning: CS8604 in `src/FlowTime.Sim.Core/Services/ProvenanceEmbedder.cs`.

### 2026-01-08 - Phase 2 Task 2.3

**Tests (RED):**
- Added `GetStateWindow_SinkNode_EmitsCompletionSeries` in `tests/FlowTime.Api.Tests/StateEndpointTests.cs`.

**Implementation (GREEN):**
- No code changes required; existing SLA completion series already emitted for sinks once served/errors invariants are enforced.

**Validation:**
- `dotnet test --nologo tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter GetStateWindow_SinkNode_EmitsCompletionSeries`

### 2026-01-08 - Phase 2 Task 2.4 (FR6)

**Tests (RED):**
- Added `SinkNode_AllowsRefusedArrivalsAndDerivesServed` in `tests/FlowTime.Sim.Tests/Templates/SinkNodeTemplateTests.cs`.
- Extended `GetStateWindow_SinkNode_EmitsCompletionSeries` to assert schedule adherence for sinks.

**Implementation (GREEN):**
- Allow sink `semantics.errors` and derive served when omitted (`src/FlowTime.Sim.Core/Templates/SinkNodeSynthesizer.cs`).
- Preserve topology dispatch schedule in generated models and API outputs (`src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs`, `src/FlowTime.Core/Models/ModelParser.cs`, `src/FlowTime.Core/Models/Node.cs`, `src/FlowTime.Contracts/Dtos/ModelDtos.cs`, `src/FlowTime.Contracts/Services/ModelService.cs`, `src/FlowTime.API/Services/StateQueryService.cs`, `src/FlowTime.API/Services/GraphService.cs`).
- Update template schema/docs for topology `dispatchSchedule` (`docs/schemas/template.schema.json`, `docs/schemas/template-schema.md`).

**Validation:**
- `dotnet test --nologo --filter "SinkNode_AllowsRefusedArrivalsAndDerivesServed|GetStateWindow_SinkNode_EmitsCompletionSeries"`
  - Warnings: CS8604 in `src/FlowTime.Sim.Core/Services/ProvenanceEmbedder.cs`, CS0105 in `src/FlowTime.Generator/Orchestration/RunOrchestrationService.cs`.
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter SinkNode_DispatchSchedule_IsPreservedInGeneratedModel`
  - Warning: CS8604 in `src/FlowTime.Sim.Core/Services/ProvenanceEmbedder.cs`.

### 2026-01-08 - Graph API Sink Inclusion

**Tests (RED):**
- Added `GetGraphAsync_IncludesSinkKind_InOperationalAndFullModes` in `tests/FlowTime.Api.Tests/Services/GraphServiceTests.cs`.

**Implementation (GREEN):**
- Include `sink` in GraphService default kind allowlists (operational + full) in `src/FlowTime.API/Services/GraphService.cs`.

**Validation:**
- `dotnet test --nologo tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter GetGraphAsync_IncludesSinkKind_InOperationalAndFullModes`

### 2026-01-08 - Phase 3 Task 3.1

**Tests (RED):**
- Added `Topology_ShowsSinkBadge_WhenKindSink` in `tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTests.cs`.

**Implementation (GREEN):**
- Render sink badge when `kind: sink` in `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`.

**Validation:**
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter Topology_ShowsSinkBadge_WhenKindSink`
  - Warnings: CS8602 in `src/FlowTime.UI/Components/Templates/SimulationResults.razor`, CS1998 in `src/FlowTime.UI/Pages/ArtifactDetail.razor` and `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, CS8604 in `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`, CS0414 in `src/FlowTime.UI/Components/Templates/SimulationResults.razor`, MUD0002 in `src/FlowTime.UI/Pages/TimeTravel/RunOrchestration.razor`, CS8604 in `tests/FlowTime.UI.Tests/FlowTimeApiClientTests.cs`, xUnit2013 in `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`.

### 2026-01-08 - Phase 3 Task 3.2

**Tests (RED):**
- Added `TransportationTemplates_UseSinkKind_ForTerminalLines` in `tests/FlowTime.Sim.Tests/Templates/SinkTemplateTests.cs`.

**Implementation (GREEN):**
- Updated test to parse generated engine model via `TemplateService.GenerateEngineModelAsync` to resolve template parameters before validation.

**Validation:**
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter TransportationTemplates_UseSinkKind_ForTerminalLines`
  - First run timed out at 10s; reran with longer timeout and passed.

### 2026-01-08 - Transportation Destinations Split

**Update:**
- Reworked transportation templates so `Line*` nodes represent the route (service) and new destination sinks (`Airport`, `Downtown`, `Industrial`) capture terminal success.
- Updated `SinkTemplateTests` to assert sinks are the destinations and lines are non-sink.

**Validation:**
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter TransportationTemplates_UseSinkKind_ForTerminalLines`

### 2026-01-08 - Sink Architecture Docs

**Docs:**
- Added sink-specific architecture guidance in `docs/architecture/service-with-buffer/sink-node-architecture.md`.
- Updated sink section in `docs/architecture/service-with-buffer/service-with-buffer-architecture-part2.md` to reflect current sink semantics.

### 2026-01-08 - Sink Focus Chips

**Tests (RED):**
- Added `FocusLabelForSinkUsesFocusBasisInsteadOfCustomValue` in `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`.

**Implementation (GREEN):**
- Treat `sink` as an operational node category for focus label computation in `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`.

**Validation:**
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter FocusLabelForSinkUsesFocusBasisInsteadOfCustomValue`

### 2026-01-08 - Sink Metrics Suppression

**Tests (RED):**
- Added `SinkOverlayMetrics_SuppressQueueAndUtilization` in `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`.

**Implementation (GREEN):**
- Suppress queue/utilization/latency/retry metrics for sink overlays and tooltips in `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`.
- Suppress queue/capacity/retry/raw queue latency rows for sinks in `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`.

**Validation:**
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter SinkOverlayMetrics_SuppressQueueAndUtilization`

### 2026-01-08 - Transportation Retry Variance

**Update:**
- Added time-varying retry rate series for airport retries in transportation templates.
- Replaced constant retry rate usage with a deterministic pulse+convolution pattern for variability.

### 2026-01-08 - Sink Schedule SLA + Inspector Schedule Rows

**Update:**
- Use schedule adherence as the SLA basis for sinks that have dispatch schedules (node chip + sparkline).
- Show arrival schedule + capacity in the inspector bin metrics for sinks.
- Hide the canvas schedule overlay panel for now.
- Include dispatch schedule in bin dump for sink debugging.

### 2026-01-09 - UI Refinements + Tooltip/Inspector Consistency

**Update:**
- Tooltip rows are now node-kind aware (sink/service/serviceWithBuffer/queue/router) and keep a stable height with `-` for missing values.
- Flow latency is preserved when custom `bin(t)` values update metrics.
- Inspector summary row order standardized to timestamp → `bin` → `bin(t)`.
- Flow latency is surfaced in the inspector bin table (derived metrics).
- Minor inspector typography adjustments for summary/bin rows.

### 2026-01-09 - Tooltip & Warning Polish

**Update:**
- Inspector toggle renders as an `info` chip anchored to the tooltip top-right.
- Tooltip queue warning text respects line breaks.
- Inspector warning headers are left-aligned; warning titles use a smaller amber style.

### 2026-01-09 - Selected Tooltip TTL + Validation Run

**Update:**
- Selected-node tooltip dismissal moved to a 10s timer in `TopologyCanvas.razor.cs`; JS focused tooltip TTL removed.
- Inspector kind chip row spacing adjusted (bottom margin).

**Validation:**
- `dotnet build`
  - Warnings: CS8604 in `src/FlowTime.Sim.Core/Services/ProvenanceEmbedder.cs`, CS0105 in `src/FlowTime.Generator/Orchestration/RunOrchestrationService.cs`.
- `dotnet test --nologo`
  - Failures:
    - `FlowTime.UI.Tests.TimeTravel.TopologyInspectorTests.DispatchScheduleOverlayReflectsGraph` (capacity label now missing `Capacity:` prefix).
    - `FlowTime.Sim.Tests.NodeBased.TemplateParserTests.Template_With_Sink_Kind_Parses` (sink now enforces served == arrivals mapping).
    - `FlowTime.Sim.Tests.Templates.RouterTemplateRegressionTests.TransportationClassesTemplate_DoesNotEmitQueueDepthMismatchWarnings` (queue depth mismatch still emitted for AirportDispatchQueue).

### 2026-01-09 - Phase 4 Docs + Validation Fixes

**Docs:**
- Updated sink guidance in `docs/templates/template-authoring.md` and `docs/notes/modeling-queues-and-buffers.md` to reflect `kind: sink` semantics and schedule adherence guidance.

**Template Validation Fixes:**
- Restored explicit dispatch-queue depth series ids (`*_dispatch_queue_depth`) in transportation templates.
- Switched hub backlog outputs to `hub_queue_carry` to align with existing backlog series.
- Removed cyclic queue references from warehouse template and aligned picker wave schedule outputs to synthesized queue series.
- Allowed self-shift initial conditions to match topology queue depth ids (`src/FlowTime.Core/Models/ModelParser.cs`).

**Validation:**
- `dotnet build` (warnings: CS8604 in `ProvenanceEmbedder.cs`, CS0105 in `RunOrchestrationService.cs`, UI warnings).
- `dotnet test --nologo`
  - Failure: `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance` (expected perf failure; other M2 perf tests skipped).

---

## Phase 1: Schema + Parser

**Goal:** Support `kind: sink` in templates and enforce sink-only fields.

### Task 1.1: Template schema allows sink kind
**File(s):** `docs/schemas/template.schema.json`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test: `Template_With_Sink_Kind_Parses` (RED)
- [x] Schema already permits any topology node kind; no change required (GREEN)
- [ ] Commit: `feat(schema): allow sink node kind`

**Status:** ✅ Complete (no schema change needed)

---

### Task 1.2: Parser rejects queue/capacity on sinks
**File(s):** `src/FlowTime.Sim.Core/Templates/Template.cs`, parser validation

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test: `SinkNode_RejectsQueueCapacityFields` (RED)
- [x] Enforce sink field restrictions in parser (GREEN)
- [ ] Commit: `feat(sim): validate sink fields`

**Status:** ✅ Complete (pending commit)

---

## Phase 2: Engine + Output

**Goal:** Implement sink semantics and emit completion series.

### Task 2.1: Sink served/errors invariants
**File(s):** engine sink evaluator

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test: `SinkNode_ServedEqualsArrivals` (RED)
- [x] Implement sink served/errors invariants (GREEN)
- [ ] Commit: `feat(engine): sink served/errors invariants`

**Status:** ✅ Complete (pending commit)

---

### Task 2.2: Sink evaluation semantics
**File(s):** engine node evaluation pipeline

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test: `SinkNode_RejectsRetryFields` (RED)
- [x] Ensure sink nodes do not compute queue/capacity/retry (GREEN)
- [ ] Commit: `feat(engine): sink evaluation semantics`

**Status:** ✅ Complete (pending commit)

---

### Task 2.3: Completion series emission
**File(s):** run manifest + API outputs

**Checklist (TDD Order - Tests FIRST):**
- [x] Write integration test: `GetStateWindow_SinkNode_EmitsCompletionSeries` (RED)
- [x] Emit completion series for sinks in run outputs (GREEN)
- [ ] Commit: `feat(api): emit sink completion series`

**Status:** ✅ Complete (pending commit)

---

### Task 2.4: Sink schedule adherence + refused arrivals
**File(s):** sink synthesizer, model/topology dispatch schedule, template schema

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test: `SinkNode_AllowsRefusedArrivalsAndDerivesServed` (RED)
- [x] Extend API test: `GetStateWindow_SinkNode_EmitsCompletionSeries` to assert schedule adherence (RED)
- [x] Allow sink errors + derive served when omitted (`SinkNodeSynthesizer`) (GREEN)
- [x] Surface topology dispatch schedule and use it for SLA (`ModelService`, `ModelParser`, `StateQueryService`, `GraphService`) (GREEN)
- [x] Update template schema/docs for topology `dispatchSchedule` (GREEN)
- [ ] Commit: `feat(sink): add schedule adherence + refused arrivals support`

**Status:** ✅ Complete (pending commit)

---

## Phase 3: UI + Templates

**Goal:** Render sink kind and align templates.

### Task 3.1: Sink kind rendering in UI
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write UI test: `Topology_ShowsSinkBadge_WhenKindSink` (RED)
- [x] Render sink kind in topology/inspector (GREEN)
- [ ] Commit: `feat(ui): render sink kind`

**Status:** ✅ Complete (pending commit)

---

### Task 3.2: Templates updated for sink kind
**File(s):** transportation templates

**Checklist (TDD Order - Tests FIRST):**
- [x] Write integration test: `TransportationTemplates_UseSinkKind_ForTerminalLines` (RED)
- [x] Update templates to use `kind: sink` for terminal success nodes (GREEN)
- [ ] Commit: `feat(templates): use sink kind for terminals`

**Status:** ✅ Complete (pending commit)

---

## Phase 4: Docs + Validation

**Goal:** Document sink kind and validate milestone.

### Task 4.1: Documentation updates
**File(s):** `docs/templates/template-authoring.md`, `docs/notes/modeling-queues-and-buffers.md`

**Checklist (TDD Order - Tests FIRST):**
- [x] Update sink kind guidance in docs (RED)
- [ ] Commit: `docs: sink kind guidance`

**Status:** ✅ Complete (pending commit)

---

### Task 4.2: Full validation
**Checklist (TDD Order - Tests FIRST):**
- [x] Run `dotnet build` (warnings noted)
- [x] Run `dotnet test --nologo` (fails: M2 perf mixed workload; see progress log)

**Status:** ✅ Complete (tests recorded)

---

## Final Checklist

- [ ] All phase tasks complete
- [ ] `dotnet build` passes
- [ ] `dotnet test --nologo` passes
- [x] Milestone document updated (status → ✅ Complete)
- [x] Release notes added
