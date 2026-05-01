# CL-M-04.03.01 — Router Nodes & Class Routing Validation

**Status:** 📋 Planned  
**Dependencies:** ✅ CL-M-04.03 (UI class visualization), ✅ CL-M-04.02 (engine class aggregation)  
**Target:** Introduce a router node type and analyzer coverage so class-tagged flows route deterministically through the DAG without template gymnastics.

---

## Overview

UI selectors and engine aggregations are ready, but templates still rely on scalar split expressions when a queue feeds multiple destinations. This milestone adds an explicit router node type, updates the template schema/engine to honor class-preserving routes, and expands analyzers so “classCoverage: full” genuinely guarantees no cross-class leakage.

### Strategic Context
- **Motivation:** Prevent class mixing caused by percentage-based splits, simplify templates, and guarantee routers conserve class volumes.
- **Impact:** Templates can declare routers declaratively; engine/analyzers enforce per-class conservation and UI selectors reflect true routing behavior.
- **Dependencies:** Builds directly on CL‑M‑04.03 (UI) & CL‑M‑04.02 (engine class data). No CL‑M‑04.04 dependencies.

---

## Scope

### In Scope ✅
1. Schema update + engine support for a `router` node that routes arrivals from one upstream component to N downstream targets (with either explicit class lists or probability weights).
2. Template updates (transportation + supply chain) to replace expression-based splits with router nodes; adjust queues/services accordingly.
3. Class analyzer enhancements: detect class leakage at routers, validate per-class conservation, emit actionable warnings.
4. Telemetry/state endpoints updated to surface router metadata (so UI can show class routes in inspectors/filter results). Router nodes must be visually distinguishable from services/queues (e.g., unique glyph or icon) so operators can spot routing behavior at a glance.

### Out of Scope ❌
- ❌ Multi-hop routing algorithms (just single-hop routers; nested logic stays out).
- ❌ Router UI/visual embellishments beyond class chips (dedicated router icon can wait).
- ❌ Telemetry ingestion/routing (handled in CL‑M‑04.04 epic).

---

## Requirements

### Functional Requirements

#### FR1: Router Schema & Engine Support
- YAML schema (`docs/schemas/model.schema.yaml`) recognizes `kind: router`, with:
  - `inputs.queue`: upstream queue/series
  - `routes[]`: each route has `target`, optional `classes`, optional `weight` (weights normalized if no class filter)
- Evaluation engine consumes a router node:
  - If `classes` defined: look up those class series and send 100% of that class to the target.
  - If no classes: use `weight` to split remaining totals proportionally, still tracking per-class contributions.
  - Router preserves conservation: total arrivals = sum of routed flows each bin.

#### FR2: Template Refactor
- Transportation template uses a router after `HubQueue` to send riders to Airport/Downtown/Industrial lines without percentage math; remove scalar splits.
- Supply-chain template uses routers for returns/dlq branching so class coverage remains consistent past returns queue.
- Document router usage in template docs + README.

#### FR3: Analyzer Coverage
- Extend class analyzer to:
  - Flag routers where the sum of routed class flows ≠ input total (per class and total).
  - Warn when a class hits a router with no matching route (drops to “default” path).
  - Export router diagnostics to API logs (similar to coverage logs).
- Provide CLI command (e.g., `flowtime analyzers --target router-classes`) to run these checks.

#### FR4: API / UI Metadata
- `/runs/{id}/state_window` includes router metadata so UI can display routing info if needed.
- Ensure `classCoverage` remains accurate for router nodes; if a router drops class metadata, analyzer + API logs reflect that.

### Non-Functional Requirements
- Routers must be deterministic: no randomness or per-class weighting beyond what’s declared.
- Performance: router evaluation should be linear in the number of routes; no re-simulation per class.
- Backward compatibility: templates without router nodes continue to work; router is additive.

---

## Implementation Plan

### Phase 1: Schema & Engine (Router Node)
- RED: add failing unit tests covering router conservation + class routing (`tests/FlowTime.Core.Tests/RouterNodeTests.cs`).
- GREEN: update schema, `ModelParser`, `ClassContributionBuilder`, etc. to recognize router nodes and compute per-class outputs.

### Phase 2: Template Updates & Docs
- RED: template tests verifying router output (FlowTime.Sim tests + integration snapshot).
- GREEN: update `transportation-basic-classes.yaml` and `supply-chain-multi-tier-classes.yaml` to use routers; regenerate sample runs; update docs/README with router guidance.

### Phase 3: Analyzer & API Integration
- RED: analyzer tests verifying router warnings (new analyzer harness).
- GREEN: implement router analyzer, update API logging (StateQueryService) to log router diagnostics, add CLI analyzer command.
- Re-run analyzer on sample runs; document outputs in tracker.

---

## Test Plan

### TDD Strategy
- Each major change (router node, template, analyzer) begins with failing unit/integration tests before implementation.

### Test Cases
- **Engine:** `RouterNodeRoutesClassesCorrectly`, `RouterNodeFallsBackToWeights`.
- **Templates:** golden runs to ensure `classCoverage: full` and routes reflect intended classes.
- **Analyzer:** `RouterAnalyzer_FlagsClassLeakage`, `RouterAnalyzer_PassesClassMatchedRoutes`.
- **API smoke:** ensure `/state_window` responses include router nodes without regressions.

---

## Success Criteria
- [ ] Router node defined in schema + supported by engine/analyzer.
- [ ] Transportation & supply-chain templates use router nodes; regenerated runs show clean class coverage (no leakage).
- [ ] Analyzer/CLI flags (or confirms) router class conservation.
- [ ] Docs/templates updated to guide authors on router usage.

---

## File Impact Summary

### Major Changes
- `docs/schemas/model.schema.yaml` — router schema definition.
- `src/FlowTime.Core/*` — router node parsing/evaluation/class propagation.
- `templates/*-classes.yaml` — router-based dispatch.
- `docs/templates/*.md` — router authoring instructions.
- Analyzer harness & CLI (`src/FlowTime.Cli/…` if needed) — router validation.

### Minor Changes
- `src/FlowTime.UI/…` (optional) to display router metadata when available.

---

## Migration Guide

### Breaking Changes
- None (router is optional). Existing templates continue to function.

### Adoption Notes
- To get precise class routing, convert percentage-based splits to router nodes. Analyzer tooling will flag routers that still leak classes into unintended paths.
