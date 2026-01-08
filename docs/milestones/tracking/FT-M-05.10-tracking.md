# FT-M-05.10 Implementation Tracking

**Milestone:** FT-M-05.10 — Sink Node Role (Success Terminal)  
**Started:** 2026-01-07  
**Status:** 🔄 In Progress  
**Branch:** `milestone/ft-m-05.10`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** `docs/milestones/FT-M-05.10-sink-node-role.md`
- **Related Analysis:** `docs/architecture/service-with-buffer/service-with-buffer-architecture-part2.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Schema + Metadata Plumbing (3/3 tasks)
- [x] Phase 2: UI Rendering + Suppression (3/3 tasks)
- [ ] Phase 3: Docs + Validation (2/3 tasks)

### Test Status
- **Build:** Not run
- **Tests:** Not run

---

## Progress Log

### 2026-01-07 - Session Start

**Preparation:**
- [x] Read milestone document
- [x] Read related architecture doc
- [x] Create milestone branch
- [x] Create tracking document

**Next Steps:**
- [x] Begin Phase 1
- [ ] Start Task 1.1 (schema acceptance tests)

### 2026-01-07 - Phase 1 Kickoff

**Changes:**
- Added RED test `Template_With_Sink_NodeRole_Parses` in `tests/FlowTime.Sim.Tests/NodeBased/TemplateParserTests.cs`.
- Allowed `nodeRole: sink` in `docs/schemas/template.schema.json`.
- Added RED test `CanonicalModelWriter_Preserves_NodeRole` in `tests/FlowTime.Sim.Tests/Templates/CanonicalModelWriterTests.cs`.
- Preserved `nodeRole` in `TemplateTopologyNode` and `SimModelBuilder` cloning.
- Added `nodeRole` plumbing in `ModelDtos`, `ModelService`, `TopologyNodeDefinition`, `Node`, and fixtures.

**Tests:**
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter Template_With_Sink_NodeRole_Parses`
  - Warnings: `FlowTime.Sim.Core/ProvenanceEmbedder.cs` CS8604, `FlowTime.Generator/RunOrchestrationService.cs` CS0105.
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter CanonicalModelWriter_Preserves_NodeRole`
  - Warnings: `FlowTime.Sim.Core/ProvenanceEmbedder.cs` CS8604, `FlowTime.Generator/RunOrchestrationService.cs` CS0105.
- `dotnet test --nologo tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter GetGraphAsync_EmitsNodeRole`
  - Warnings: `FlowTime.Sim.Core/ProvenanceEmbedder.cs` CS8604, `FlowTime.Generator/RunOrchestrationService.cs` CS0105.

**Next Steps:**
- [ ] Commit Task 1.1/1.2/1.3 changes.

### 2026-01-07 - Phase 2 Task 2.1

**Tests (RED):**
- Added `Topology_ShowsSinkBadge_WhenNodeRoleSink` in `tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTests.cs`.

**Validation:**
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter FullyQualifiedName~Topology_ShowsSinkBadge_WhenNodeRoleSink`
  - Warnings: CS1998 in `src/FlowTime.UI/Pages/ArtifactDetail.razor`, CS8602 in `src/FlowTime.UI/Components/Templates/SimulationResults.razor`, CS8604 in `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`, CS1998 in `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, CS0414 in `src/FlowTime.UI/Components/Templates/SimulationResults.razor`, MUD0002 in `src/FlowTime.UI/Pages/TimeTravel/RunOrchestration.razor`, CS8604 in `tests/FlowTime.UI.Tests/FlowTimeApiClientTests.cs`, xUnit2013 in `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`.

### 2026-01-07 - Phase 2 Task 2.2

**Tests (RED):**
- Added `BuildInspectorMetrics_SinkNode_SuppressesUtilizationAndErrorRateWhenMissing` in `tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTests.cs`.
- Added `BuildInspectorMetrics_SinkNode_IncludesUtilizationAndErrorRateWhenPresent` in `tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTests.cs`.

**Validation:**
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter FullyQualifiedName~SinkNode`
  - Warnings: CS8604 in `tests/FlowTime.UI.Tests/FlowTimeApiClientTests.cs`, xUnit2013 in `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`.

### 2026-01-07 - Phase 3 Docs

**Docs:**
- Added sink role guidance in `docs/templates/template-authoring.md`.
- Added sink role modeling note in `docs/notes/modeling-queues-and-buffers.md`.
- Drafted release notes in `docs/releases/FT-M-05.10.md`.

### 2026-01-07 - Diagnostics + Sparkline Refresh

**Tests (RED):**
- Added `SparklineUpdatesTriggerSceneRender` in `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`.
- Added `BinDump_UsesClassSelectionAndUnfilteredSeries` in `tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTests.cs`.

**Implementation (GREEN):**
- Scene signature now includes sparkline values so sparklines refresh while scrubbing (`src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`).
- Added inspector "Dump bin" action and bin dump payload (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `src/FlowTime.UI/Pages/TimeTravel/Topology.TestHooks.cs`).

**Validation:**
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter FullyQualifiedName~SparklineUpdatesTriggerSceneRender`
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter FullyQualifiedName~BinDump_UsesClassSelectionAndUnfilteredSeries`
  - Warnings: CS8604 in `tests/FlowTime.UI.Tests/FlowTimeApiClientTests.cs`, xUnit2013 in `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`.

---

## Phase 1: Schema + Metadata Plumbing

**Goal:** Add `nodeRole: sink` to templates and ensure it propagates into run manifests/state outputs.

### Task 1.1: Template schema acceptance
**File(s):** `docs/schemas/template.schema.json`, tests under `tests/FlowTime.Sim.Tests`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test: `TemplateSchema_AllowsNodeRoleSink` (RED)
- [x] Update template schema to allow `nodeRole: sink` (GREEN)
- [ ] Commit: `feat(schema): allow sink node role`

**Status:** ✅ Complete (pending commit)

---

### Task 1.2: Template parsing + manifest propagation
**File(s):** `src/FlowTime.Sim.Core/Templates/Template.cs`, `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs`, `src/FlowTime.Contracts/Dtos/ModelDtos.cs`, `src/FlowTime.Contracts/Services/ModelService.cs`, `src/FlowTime.Core/Models/ModelParser.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test: `CanonicalModelWriter_Preserves_NodeRole` (RED)
- [x] Preserve nodeRole in canonical model output (GREEN)
- [ ] Commit: `feat(core): propagate sink node role`

**Status:** ✅ Complete (pending commit)

---

### Task 1.3: API contract exposure
**File(s):** `src/FlowTime.Contracts/TimeTravel/GraphContracts.cs`, `src/FlowTime.API/Services/GraphService.cs`, `tests/FlowTime.Api.Tests/Services/GraphServiceTests.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test: `GetGraphAsync_EmitsNodeRole` (RED)
- [x] Expose nodeRole in graph contracts + service (GREEN)
- [ ] Commit: `feat(api): expose sink node role metadata`

**Status:** ✅ Complete (pending commit)

---

### Phase 1 Validation
- [ ] Build passes
- [ ] Schema + manifest tests pass

---

## Phase 2: UI Rendering + Suppression

**Goal:** Render sink nodes as terminals and suppress misleading metrics without changing engine logic.

### Task 2.1: Sink badge rendering
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, UI tests

**Checklist (TDD Order - Tests FIRST):**
- [x] Write UI test: `Topology_ShowsSinkBadge_WhenNodeRoleSink` (RED)
- [x] Render sink badge in topology and inspector (GREEN)
- [ ] Commit: `feat(ui): render sink node badge`

**Status:** ✅ Complete (pending commit)

---

### Task 2.2: Metric suppression rules
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write UI test: `BuildInspectorMetrics_SinkNode_SuppressesUtilizationAndErrorRateWhenMissing` (RED)
- [x] Suppress error/utilization chips unless series exist (GREEN)
- [ ] Commit: `feat(ui): suppress sink metrics when absent`

**Status:** ✅ Complete (pending commit)

---

### Task 2.3: Explicit series overrides
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write UI test: `BuildInspectorMetrics_SinkNode_IncludesUtilizationAndErrorRateWhenPresent` (RED)
- [x] Honor explicit error/utilization series for sinks (GREEN)
- [ ] Commit: `feat(ui): allow sink metrics when provided`

**Status:** ✅ Complete (pending commit)

---

### Phase 2 Validation
- [ ] UI tests pass
- [ ] Manual check: sink badge visible; metrics suppressed when absent

---

## Phase 3: Docs + Validation

**Goal:** Document sink semantics and validate the milestone.

### Task 3.1: Documentation updates
**File(s):** `docs/templates/template-authoring.md`, `docs/notes/modeling-queues-and-buffers.md`

**Checklist (TDD Order - Tests FIRST):**
- [x] Update authoring guidance with sink role examples (RED)
- [x] Document sink semantics in modeling notes (GREEN)
- [ ] Commit: `docs: add sink role guidance`

**Status:** ✅ Complete (pending commit)

---

### Task 3.2: Full validation
**Checklist (TDD Order - Tests FIRST):**
- [ ] Run `dotnet build`
- [ ] Run `dotnet test --nologo`

**Status:** ⏳ Not Started

---

### Task 3.3: Release notes
**File(s):** `docs/releases/FT-M-05.10.md`

**Checklist (TDD Order - Tests FIRST):**
- [x] Draft release notes entry
- [ ] Commit: `docs: release FT-M-05.10`

**Status:** ✅ Complete (pending commit)

---

## Final Checklist

- [ ] All phase tasks complete
- [ ] `dotnet build` passes
- [ ] `dotnet test --nologo` passes
- [ ] Milestone document updated (status → ✅ Complete)
- [ ] Release notes added
