# FT-M-05.15 — Series Semantics Metadata (Aggregation)

**Status:** ✅ Complete  
**Dependencies:** ✅ M‑03.01 (time‑travel state contract), ✅ FT‑M‑05.12  
**Target:** Add optional per‑series semantics metadata to `/state` and `/state_window` so the UI can display aggregation/percentile meaning consistently across simulated and telemetry data.

---

## Overview

FlowTime’s UI currently infers metric meaning from formulas and engine‑derived series, but we lack a structured way to describe how a series was aggregated (average vs percentile) when telemetry supplies the data. This milestone introduces optional **series semantics metadata** in the time‑travel schema so the UI can present accurate descriptions without hardcoding assumptions.

### Strategic Context
- **Motivation**: Avoid misleading UI text when telemetry provides percentile or non‑average series.
- **Impact**: Inspector tooltips and provenance blocks can display aggregation semantics (avg, p95, p99) when present, while remaining backward compatible for legacy runs.
- **Dependencies**: Time‑travel schema already stable (M‑03.01) and metric provenance catalog (FT‑M‑05.12) in place.

---

## Scope

### In Scope ✅
1. **Schema extension**: Add optional `seriesMetadata` per node in `/state` and `/state_window`.
2. **Aggregation semantics**: Define `aggregation` (avg, sum, min, max, p50, p95, p99, unknown).
3. **Origin flag**: Optional `origin` (`derived`, `telemetry`, `model`) to clarify where semantics come from.
4. **UI surface**: Inspector provenance tooltips render aggregation when metadata exists.
5. **Docs**: Update schema docs, UI provenance docs, and template/telemetry guidance to reflect the new semantics metadata.

### Out of Scope ❌
- ❌ Computing new percentiles in the engine.
- ❌ Retrofitting historical run artifacts.
- ❌ Telemetry ingest pipeline changes beyond passing metadata through.
- ❌ Renaming existing series IDs.

### Future Work (Optional)
- Per‑series unit overrides (when telemetry is mixed units).
- Contracted semantic bundles for edge metrics (edge‑time‑bin epic).

---

## Requirements

### FR1: Series Metadata Contract
**Description:** `/state` and `/state_window` nodes may optionally declare metadata per series.

**Acceptance Criteria:**
- [ ] Schema supports `seriesMetadata: { [seriesId]: { aggregation, origin } }`.
- [ ] `aggregation` is optional and constrained to a documented enum.
- [ ] `origin` is optional and constrained to a documented enum.
- [ ] Runs without `seriesMetadata` remain valid.

**Example:**
```json
{
  "seriesMetadata": {
    "latencyMinutes": { "aggregation": "avg", "origin": "derived" },
    "flowLatencyP95Ms": { "aggregation": "p95", "origin": "telemetry" }
  }
}
```

**Error Cases:**
- Unknown aggregation values are ignored by the UI and treated as `unknown`.

### FR2: UI Surfaces Aggregation
**Description:** When a series exposes aggregation metadata, the inspector shows it in provenance tooltips.

**Acceptance Criteria:**
- [ ] Provenance tooltip includes `Aggregation: avg` when metadata exists.
- [ ] Percentile series (p95/p99) are labeled as such in the tooltip.
- [ ] If metadata is missing, UI falls back to catalog meaning text.

### FR3: Derived Series Defaults
**Description:** Engine‑derived metrics set `origin=derived` and `aggregation=avg` where applicable.

**Acceptance Criteria:**
- [ ] `latencyMinutes`, `serviceTimeMs`, and `flowLatencyMs` carry `aggregation=avg` when derived.
- [ ] UI can distinguish telemetry‑provided percentiles from derived averages.

### FR4: Documentation
**Description:** Document how series semantics metadata is declared and surfaced.

**Acceptance Criteria:**
- [ ] Schema docs describe `seriesMetadata` shape and enums.
- [ ] UI provenance docs describe aggregation display rules.
- [ ] Template/telemetry guidance explains how to supply semantics metadata for emitted series.

### NFR1: Backward Compatibility
**Target:** Existing runs render exactly as before if metadata is absent.
**Validation:** Run UI against legacy fixtures and confirm no schema errors.

---

## Implementation Plan

### Phase 1: Schema + Contract
**Goal:** Extend the time‑travel schema with series metadata (RED → GREEN → REFACTOR).

**Tasks:**
1. Add schema tests to assert `seriesMetadata` shape (RED).
2. Update `docs/schemas/time-travel-state.schema.json` (GREEN).
3. Refactor schema docs and references (REFACTOR).

**Success Criteria:**
- [ ] Schema validates optional metadata without breaking existing fixtures.

### Phase 2: API + DTO Plumbing
**Goal:** Surface metadata from API response models (RED → GREEN → REFACTOR).

**Tasks:**
1. Add DTO fields in `TimeTravelApiModels` (RED).
2. Plumb metadata through `StateQueryService` for derived series (GREEN).
3. Refactor serialization helpers (REFACTOR).

**Success Criteria:**
- [ ] `/state` and `/state_window` can emit metadata when available.

### Phase 3: UI Surface + Docs
**Goal:** Surface aggregation in inspector tooltips (RED → GREEN → REFACTOR).

**Tasks:**
1. Add UI tests for aggregation display (RED).
2. Render aggregation lines in provenance tooltip entries (GREEN).
3. Update UI provenance docs and milestone notes (REFACTOR).

**Success Criteria:**
- [ ] UI shows aggregation labels when metadata exists.

---

## Test Plan

### Schema Tests
- `TimeTravelStateSchema_AllowsSeriesMetadata`
- `TimeTravelStateSchema_RejectsUnknownAggregation`

### API Tests
- `/state_window` includes metadata for derived latency series.
- Metadata omitted for legacy runs.

### UI Tests
- Inspector tooltip includes `Aggregation` entry when metadata present.
- Tooltip falls back to catalog meaning when metadata missing.

---

## Success Criteria

- [ ] Series aggregation semantics are visible in the UI when supplied.
- [ ] Derived averages are labeled consistently across nodes.
- [ ] Backward compatibility maintained for old runs.

---

## File Impact Summary

### Likely Changes
- `docs/schemas/time-travel-state.schema.json`
- `src/FlowTime.UI/Services/TimeTravelApiModels.cs`
- `src/FlowTime.API/Services/StateQueryService.cs`
- `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`
- `work/epics/ui/metric-provenance.md`
- `docs/templates/template-authoring.md`

---

## Implementation Notes (UI Tweaks)

- Inspector tabs: fixed tab bar visibility, scroll only inside tab content, and added top spacing between tabs and content.
- Inspector charts: adjusted title spacing for expanded vs. compact views.
- Inspector badges: split Kind label from the kind chip; warning tab icon resized to 14px.
- Focus view: when filters are cleared, still render the focused chain instead of a blank graph.
- Tooltips: auto-dismiss extended to 15s; Escape clears selection and tooltip.
