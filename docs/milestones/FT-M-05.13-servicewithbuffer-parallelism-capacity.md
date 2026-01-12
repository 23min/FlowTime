# FT-M-05.13 — ServiceWithBuffer Parallelism + Capacity Backlog

**Status:** 📋 Planned  
**Dependencies:** ✅ FT-M-05.09, ✅ FT-M-05.11  
**Target:** Model and surface service-with-buffer parallelism (instances/workers) so capacity scales realistically, and backlog behavior is explainable in both sim and UI.

---

## Overview

ServiceWithBuffer nodes currently express capacity but not **parallelism** (instances, buses, workers, etc.). This makes backlog growth hard to interpret and limits realism in domains where capacity is the product of **per-instance throughput × concurrent instances**.

This milestone adds explicit parallelism inputs, derives effective capacity, and surfaces backlog signals in a way that maps cleanly to real telemetry (replicas, shifts, vehicles, staffing).

---

## Scope

### In Scope ✅
1. **Parallelism inputs** for serviceWithBuffer nodes (constant or series).
2. **Derived capacity** = base capacity × parallelism (or capacity series × parallelism).
3. **Backlog behavior** signals that explain sustained queue growth vs insufficient capacity.
4. **UI visibility** for instances/capacity in chips, inspector, and tooltips.
5. **Template examples** showing one continuous-flow node and one gated-release node.

### Out of Scope ❌
- ❌ Full autoscaling policy engine (this milestone only models inputs).
- ❌ Domain-specific scheduling algorithms (keep to explicit series/const inputs).
- ❌ Cross-run comparisons.

---

## Requirements

### FR1: Parallelism Inputs
**Description:** Allow serviceWithBuffer to declare parallelism via template fields.

**Acceptance Criteria**
- [ ] Schema supports `parallelism` as const/series (per-node).
- [ ] Defaults to `1` when omitted.
- [ ] Values are validated (>= 1; non-null for series).

### FR2: Effective Capacity Derivation
**Description:** Compute total capacity based on base capacity × parallelism.

**Acceptance Criteria**
- [ ] Total capacity series is computed for each bin.
- [ ] Utilization uses **effective capacity**.
- [ ] Served cannot exceed effective capacity unless explicitly allowed by modeler.

### FR3: Backlog Signals
**Description:** Provide actionable signals for backlog growth and capacity shortfall.

**Acceptance Criteria**
- [ ] Backlog growth warnings tie to insufficient capacity vs burst arrivals.
- [ ] Warnings explain if parallelism is constant or varies.
- [ ] Signal text indicates consecutive bins and expected mitigation (increase parallelism or capacity).

### FR4: UI Exposure
**Description:** Show parallelism and effective capacity in UI.

**Acceptance Criteria**
- [ ] Node chips show `Instances` (or similar) when parallelism > 1.
- [ ] Inspector shows base capacity, parallelism, and effective capacity.
- [ ] Tooltip rows include effective capacity for serviceWithBuffer.

### FR5: Template Examples
**Description:** Update one template with continuous processing and one with gated releases.

**Acceptance Criteria**
- [ ] Transportation or warehouse template shows real-world instance scaling.
- [ ] Another template (e.g., IT microservices) shows parallelism variance.

---

## Implementation Plan

### Phase 1 — Schema + Validation
1. Extend template schema to include `parallelism` for serviceWithBuffer.
2. Validate `parallelism` inputs (const/series).
3. Update template docs and examples.

### Phase 2 — Engine Computation
1. Derive effective capacity series.
2. Update utilization/served/backlog logic to use effective capacity.
3. Update analyzer signals to reference effective capacity.

### Phase 3 — UI + Inspector
1. Add node chip for instances/parallelism.
2. Update inspector rows for base capacity, parallelism, effective capacity.
3. Ensure tooltips reflect effective capacity.

### Phase 4 — Tests + Golden Data
1. Schema tests (parallelism defaults + validation).
2. Sim tests verifying capacity scaling and backlog warning behavior.
3. Update golden run outputs if required.

---

## Test Plan

### Schema Tests
- `TemplateValidator_Parallelism_DefaultsToOne`
- `TemplateValidator_Parallelism_RejectsZero`

### Sim Tests
- `ServiceWithBuffer_EffectiveCapacity_ScalesWithParallelism`
- `ServiceWithBuffer_BacklogSignals_UseEffectiveCapacity`

### UI Tests
- `Topology_ShowsParallelismChip_WhenInstancesGreaterThanOne`
- `Inspector_ShowsEffectiveCapacity`

---

## Success Criteria

- [ ] Parallelism is visible and correctly affects capacity.
- [ ] Backlog warnings are explainable in terms of capacity shortfall.
- [ ] Example templates demonstrate realistic scaling behavior.
- [ ] No regressions in SLA/backlog metrics.

---

## Notes

This milestone implements **model inputs and derived capacity**, not automatic scaling. Autoscaling logic remains a future enhancement and should be layered on top of explicit `parallelism` series.
