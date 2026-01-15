# FT-M-05.10 — Sink Node Role (Success Terminal)

**Status:** ✅ Complete  
**Dependencies:** ✅ FT-M-05.09  
**Target:** Introduce a metadata-only sink role that marks terminal success nodes and suppresses misleading error/utilization signals without changing engine behavior.

---

## Overview

Some modeled systems end with successful terminal nodes (airports, delivery endpoints, customer receipt). Treating these nodes as ordinary services can create artificial errors, utilization warnings, or SLA confusion. This milestone introduces a **metadata-only sink role** that the UI can render as a “success terminal” without changing engine logic. It keeps telemetry compatibility by requiring explicit sink declaration rather than inference.

### Strategic Context
- **Motivation:** Reduce false error/utilization signals at terminal success nodes.
- **Impact:** Clearer topology interpretation and more accurate SLA context.
- **Dependencies:** Builds on ServiceWithBuffer semantics from FT‑M‑05.09.

## Scope

### In Scope ✅
1. Add `nodeRole: sink` (or similar) metadata for templates and run manifests.
2. Update UI to render sink nodes with a success badge and suppress error-rate/utilization chips unless explicitly provided.
3. Document sink semantics and authoring guidance.
4. Add tests for sink rendering and warning suppression.

### Out of Scope ❌
- ❌ New engine behavior or `kind: sink`.
- ❌ Automatic inference of sinks from graph topology.
- ❌ Cross-node SLA or anomaly detection.

## Requirements

### Functional Requirements

#### FR1: Sink Role Metadata
**Description:** A node can be explicitly marked as a sink via metadata.

**Acceptance Criteria:**
- [x] Template schema supports optional `nodeRole: sink`.
- [x] Run manifests and API state outputs surface sink role metadata.
- [x] Missing sink role defaults to normal service behavior.

#### FR2: Sink Rendering and Metric Suppression
**Description:** UI reflects sink semantics without altering engine data.

**Acceptance Criteria:**
- [x] Sink nodes render with a “terminal success” badge (chip/label).
- [x] Error-rate/utilization chips are hidden unless errors/capacity are explicitly provided.
- [x] SLA metrics are shown only if available; no fabricated signals.

#### FR3: Documentation
**Description:** Authoring guidance explains when to use sinks.

**Acceptance Criteria:**
- [x] `docs/templates/template-authoring.md` includes sink guidance and examples.
- [x] `docs/notes/modeling-queues-and-buffers.md` clarifies sink semantics.

## Implementation Plan

### Phase 1: Schema + Metadata Plumbing
**Goal:** Add `nodeRole` to templates and manifests.

**Tasks:**
1. RED tests for schema acceptance of `nodeRole`.
2. Update template schema + parsing.
3. Ensure run metadata carries nodeRole.

### Phase 2: UI Rendering
**Goal:** Display sinks and suppress misleading metrics.

**Tasks:**
1. RED UI test for sink badge rendering.
2. RED UI tests that verify sink metrics are suppressed when missing and shown when explicit series exist.
3. Implement sink badge and the single suppression/override rule (same change covers both missing and present series).

### Phase 3: Docs + Validation
**Goal:** Document sink semantics and validate tests.

**Tasks:**
1. Update docs and examples.
2. Run `dotnet build` + `dotnet test --nologo`.

## Test Plan

### Unit Tests
1. `TemplateSchema_AllowsNodeRoleSink`
2. `RunManifest_PropagatesNodeRoleSink`

### UI Tests
1. `Topology_ShowsSinkBadge_WhenNodeRoleSink`
2. `BuildInspectorMetrics_SinkNode_SuppressesUtilizationAndErrorRateWhenMissing`
3. `BuildInspectorMetrics_SinkNode_IncludesUtilizationAndErrorRateWhenPresent`

## Success Criteria

### Milestone Complete When
- [x] Sink role supported in schema + manifests.
- [x] UI renders sink badge and suppresses misleading metrics.
- [x] Docs updated and tests passing.

## File Impact Summary

### Files to Modify (Major Changes)
- `docs/schemas/template.schema.json`
- `src/FlowTime.Sim.Core/Templates/Template.cs`
- `src/FlowTime.Contracts/Dtos/ModelDtos.cs`
- `src/FlowTime.Contracts/Services/ModelService.cs`
- `src/FlowTime.Core/Models/ModelParser.cs`
- `src/FlowTime.Core/Models/Node.cs`
- `src/FlowTime.Contracts/TimeTravel/GraphContracts.cs`
- `src/FlowTime.API/Services/GraphService.cs`
- `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`
- `src/FlowTime.UI/Components/Topology/GraphMapper.cs`

### Files to Modify (Minor Changes)
- `docs/templates/template-authoring.md`
- `docs/notes/modeling-queues-and-buffers.md`
