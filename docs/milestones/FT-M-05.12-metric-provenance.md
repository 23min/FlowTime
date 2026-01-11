# FT-M-05.12 — Metric Provenance & Audit Trail

**Status:** 📋 Planned  
**Dependencies:** ✅ FT‑M‑05.09, ✅ FT‑M‑05.11  
**Target:** Make metric calculations and data sources explicit in the UI and dump tooling so modelers and debuggers can trace “why this value”.

---

## Overview

FlowTime’s inspectors show computed metrics (SLA, utilization, queue latency, flow latency, etc.) but do not currently explain how those values were derived. This milestone adds a provenance/audit layer that:

- Exposes **formulas**, **inputs**, and **units** per metric.
- Clarifies **gating rules** (dispatch schedule bins, backlog age availability).
- Adds a **bin‑level audit view** that complements the existing value dump.

The goal is to help modelers validate assumptions and help debugging without relying on external tracing.

## Scope

### In Scope ✅
1. **Inspector provenance view** for metric rows (expandable explanation).
2. **Formula and source series mapping** per node kind (service, serviceWithBuffer, sink, dlq, router).
3. **Gating rules** (dispatch schedule, backlog age unavailability, queue‑latency gates).
4. **Units and semantics** (minutes vs ms vs percent).
5. **Bin dump augmentation** (optional provenance payload or view).
6. **Modifier‑key bin dump behavior**: open in new tab instead of file download.

### Out of Scope ❌
- ❌ New metrics or alternate computation models.
- ❌ Full anomaly detection (future epic).
- ❌ Edge‑time‑bin enhancements (future epic).
- ❌ Topology focus/drilldown view (tracked in FT‑M‑05.13).

### Candidate Modeling Improvements (Tracked in 5.12 Notes)
These are **not** implemented in 5.12, but are recorded here so provenance work can reference real-world modeling gaps:

- **Transportation:** Explicit travel-time delays between dispatch, line, and sink (now implemented for the classed template).
- **Warehouse picker:** Transfer delays from picker waves to packing/shipping stages.
- **IT document processing:** Handoff delays and queue/service distinctions across ingress/processing/egress.
- **Supply chain multi-tier:** Explicit inter-tier transit/handling delays.
- **Network reliability / manufacturing:** Model transfer/transport legs where queue vs travel is unclear.

---

## Requirements

### FR1: Inspector Metric Provenance
**Description:** Each metric row in the inspector can expand to show how the value is computed.

**Acceptance Criteria:**
- [ ] Expandable provenance row for each metric shown in the inspector.
- [ ] Displays formula (e.g., `utilization = served / capacity`).
- [ ] Displays source series IDs (e.g., `served_airport`, `cap_airport`).
- [ ] Displays gating rules (e.g., schedule bins only, backlog age unavailable).
- [ ] Displays units (%, ms, min).

### FR2: Node‑Kind Formula Catalog
**Description:** Metric formulas are defined in a single catalog by node kind.

**Acceptance Criteria:**
- [ ] A code‑level catalog maps `nodeKind → metric → formula + sources + units`.
- [ ] When a metric’s input series is missing, the provenance view states **why**.
- [ ] Catalog is deterministic (no runtime sampling beyond bin selection).

### FR3: Bin Dump Audit View
**Description:** Bin dump can be opened in a browser tab and includes provenance.

**Acceptance Criteria:**
- [ ] ALT/CTRL (configurable) opens dump in a new tab with JSON + provenance.
- [ ] Default behavior remains file download.
- [ ] Dump view includes computed values + the provenance catalog slice used.

### FR4: Queue/Latency Clarification
**Description:** Queue latency vs service time vs flow latency are explicitly explained.

**Acceptance Criteria:**
- [ ] Provenance view explains queue latency derivation for serviceWithBuffer.
- [ ] Service time shown as per‑unit processing time (ms).
- [ ] Flow latency description clearly states it is end‑to‑end (if available).
- [ ] Total time in system is explained as `queue latency + service time` (unit conversion noted).

---

## Implementation Plan

### Phase 1: Provenance Catalog + API Shape
1. Define a metric provenance catalog in UI (or shared contract) per node kind.
2. Map metrics to series IDs using node semantics + known formula rules.

### Phase 2: Inspector UX
1. Add expand/collapse affordance on inspector metric rows.
2. Render formula + inputs + units + gating status.

### Phase 3: Bin Dump Enhancements
1. Extend dump payload with provenance details.
2. Add ALT/CTRL modifier to open in new tab (read‑only viewer).

### Phase 4: Docs + Validation
1. Document provenance UX and dump behavior.
2. Add architecture note: `docs/architecture/ui/metric-provenance.md`.
3. Run `dotnet build` and `dotnet test --nologo`.

---

## Test Plan

### Unit Tests
- `MetricProvenanceCatalog_KindsHaveRequiredEntries`
- `MetricProvenance_ReportsMissingInputs`

### UI Tests
- `Inspector_ExpandsMetricProvenance`
- `BinDump_AltKeyOpensTab`

---

## Success Criteria

- [ ] Users can see formulas and inputs for each metric.
- [ ] Bin dump supports both file download and tab view.
- [ ] Latency semantics are unambiguous in the UI.

---

## File Impact Summary

### Likely Changes
- `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`
- `src/FlowTime.UI/Pages/TimeTravel/Topology.TestHooks.cs`
- `src/FlowTime.UI/Services/TimeTravelApiModels.cs`
- `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`
- `docs/architecture/ui/metric-provenance.md`
- `docs/ui/*` or `docs/notes/*` for metric semantics
