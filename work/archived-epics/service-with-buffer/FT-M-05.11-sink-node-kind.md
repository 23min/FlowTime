# FT-M-05.11 — Sink Node Kind (Terminal Success Semantics)

**Status:** ✅ Complete  
**Dependencies:** ✅ FT-M-05.10  
**Target:** Introduce a first-class `kind: sink` node so terminal success has explicit engine semantics and completion series.

---

## Overview

While FT‑M‑05.10 introduces a metadata-only sink role for UI clarity, some domains need explicit terminal success semantics in the engine. A `kind: sink` node ensures arrivals terminate successfully, enables explicit completion series, and supports SLA calculations without misusing errors or capacity on leaf services.

### Strategic Context
- **Motivation:** Avoid modeling terminal success nodes as capacity-limited services and provide explicit completion series for SLA.
- **Impact:** Engine gains a new node kind; templates and telemetry mappings can represent success terminals explicitly.
- **Dependencies:** Requires FT‑M‑05.10 so UI rendering and metadata plumbing exist before engine semantics change.

## Scope

### In Scope ✅
1. Add `kind: sink` to the template schema and parsing.
2. Implement engine semantics: `served = arrivals`, `errors = 0`, no queue or capacity.
3. Emit completion series for sinks (for SLA).
4. Update API/manifest outputs to include sink nodes explicitly.
5. Update UI to render sink kind (if not already covered by nodeRole).
6. Update templates where terminal success is appropriate.
7. Define sink-friendly metrics/visual behavior (subset of service metrics, no queue/capacity).
8. Support schedule adherence and optional refused-arrivals telemetry for sinks.
9. Add time-varying retry rates in Transportation templates to reflect operational variability.

### Out of Scope ❌
- ❌ Automatic inference of sinks from topology.
- ❌ Cross-node SLA logic or anomaly clustering.

## Implementation Notes (In-Flight)

- Tooltips now render per node kind (sink/service/serviceWithBuffer/queue/router), with only relevant rows and stable heights.
- Inspector summary rows are ordered as timestamp → `bin` → `bin(t)` for consistent scanning.
- Bin dumps include sink dispatch schedule metadata for traceability.
- Flow latency retention is fixed when `bin(t)` values update node metrics.

## Requirements

### Functional Requirements

#### FR1: Schema + Parsing
**Description:** Templates can declare `kind: sink`.

**Acceptance Criteria:**
- [ ] Template schema allows `kind: sink`.
- [ ] Template parser validates sink nodes and rejects queue/capacity fields on sinks.

#### FR2: Engine Semantics
**Description:** Sink nodes terminate items successfully.

**Acceptance Criteria:**
- [ ] `served == arrivals` per bin for sinks.
- [ ] `errors == 0` unless explicit error series is provided (should be rejected by schema).
- [ ] No queue depth, no capacity, no retries for sinks.

#### FR3: Completion Series
**Description:** Sinks emit completion series used for completion SLA.

**Acceptance Criteria:**
- [ ] Completion series is emitted for sinks in run outputs.
- [ ] SLA calculations can reference sink completion series.

#### FR4: Template Alignment
**Description:** Update templates where terminal success nodes exist.

**Acceptance Criteria:**
- [ ] Transportation lines (Airport/Downtown/Industrial) use `kind: sink` where appropriate.
- [ ] Documentation explains when to use sink vs service.
- [ ] Transportation templates use time-varying retry rates for airport retries.

#### FR5: Sink Metric Subset + Focus Behavior
**Description:** Sinks should surface useful terminal metrics without becoming full services.

**Acceptance Criteria:**
- [ ] Sinks respond to focus chips for relevant metrics (SLA, error rate, flow latency, service time if present).
- [ ] Sinks never display queue depth or capacity.
- [ ] Tooltip/inspector clarifies terminal semantics (e.g., "Arrived at destination").

#### FR6: Schedule Adherence + Refused Arrivals (Optional)
**Description:** Sinks should support terminal-specific telemetry.

**Acceptance Criteria:**
- [ ] When sinks have dispatch schedules, the SLA chip/sparkline reflects schedule adherence (not completion).
- [ ] Inspector shows arrival schedule details for sinks (schedule + capacity).
- [ ] Bin dump includes sink dispatch schedule metadata for debugging.
- [ ] Schedule panel overlay on the canvas is hidden for now to avoid ambiguity.
- [ ] Optional refused-arrivals telemetry can be emitted and displayed without introducing retry/queue semantics.

## Implementation Plan

### Phase 1: Schema + Parser
1. RED tests for `kind: sink` schema validation.
2. Update template schema + parser to allow sink kind and reject invalid fields.

### Phase 2: Engine + Output
1. RED tests for sink served/errors invariants.
2. Implement sink evaluation semantics.
3. Emit completion series in manifests and API outputs.

### Phase 3: UI + Templates
1. Update UI rendering to treat sink kind as terminal success (if not already via nodeRole).
2. Update templates to use sink kind where needed.
3. Add time-varying retry-rate series in transportation templates (deterministic variability).

### Phase 4: Docs + Validation
1. Update template authoring and modeling notes.
2. Add sink-specific architecture guidance (terminal metrics, schedule adherence, refused arrivals).
3. Run `dotnet build` and `dotnet test --nologo`.

## Test Plan

### Unit Tests
- `TemplateSchema_AllowsSinkKind`
- `SinkNode_RejectsQueueCapacityFields`
- `SinkNode_ServedEqualsArrivals`

### Integration Tests
- `RunOutputs_IncludeSinkCompletionSeries`
- `TransportationLines_RenderAsSinks`

### UI Tests
- `Topology_RendersSinkKindBadge`

## Success Criteria

### Milestone Complete When
- [ ] Sink kind supported in schema + engine.
- [ ] Completion series emitted for sinks.
- [ ] Templates updated and warnings cleared.
- [ ] Docs updated and tests passing.

## File Impact Summary

### Files to Modify (Major Changes)
- `docs/schemas/template.schema.json`
- `src/FlowTime.Sim.Core/Templates/Template.cs`
- `src/FlowTime.Sim.Core/Model/*`
- `src/FlowTime.API/Services/StateQueryService.cs`
- `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

### Files to Modify (Minor Changes)
- `docs/templates/template-authoring.md`
- `docs/notes/modeling-queues-and-buffers.md`
