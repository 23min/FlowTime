---
id: G-016
title: Rust Engine Parity — Evaluation Core Gaps
status: open
---

### Why this is a gap

The Rust matrix engine (E-20) handles compilation, evaluation, and basic artifact writing for simple models, but cannot replace the C# evaluation pipeline for models that use classes, edges, or output filtering. These are evaluation-layer gaps per D-044 — the engine core must return complete results before the artifact sink or consumers can use them.

### Gaps (engine core — must be in Rust)

| Gap | Severity | Status | Notes |
|-----|----------|--------|-------|
| Per-class column decomposition | Critical | Deferred (M-033, matrix-engine.md) | Rust computes class routing internally (`__class_` columns) but does not expose per-class series in EvalResult. C# uses ClassContributionBuilder. |
| Edge series materialization | High | Undocumented | C# EdgeFlowMaterializer produces per-edge throughput/attempt series. Rust uses edges for ordering only. |
| `outputs:` filtering/renaming | Medium | Undocumented | `OutputDefinition` parsed in model.rs but never used in compiler or writer. Models use this to select/rename output series. |

### Gaps (artifact sink — separate from engine core)

| Gap | Severity | Status | Notes |
|-----|----------|--------|-------|
| `model/model.yaml` | Low | Undocumented | Copy of input model in run directory. |
| `model/metadata.json` | Medium | Undocumented | Template metadata, telemetry sources, mode. Needs metadata extraction from YAML. |
| `model/provenance.json` | Low | Partially documented (D-043) | Sim-specific provenance deferred until bridge exercised by real Sim runs. |
| `spec.yaml` at run root | Low | Undocumented | Normalized topology with file:// URIs. Needed by StateQueryService. |
| Per-class CSV naming (`{node}@{component}@{class}.csv`) | High | Deferred (M-033) | Depends on per-class decomposition in engine core. |
| Edge CSV naming (`edge_{id}_{metric}@{component}@{class}.csv`) | Medium | Undocumented | Depends on edge series in engine core. |
| Full `series/index.json` schema (kind, unit, class, componentId, hash) | Medium | Undocumented | Current Rust schema is minimal. |
| Full `run.json` schema (classes, classCoverage, source, inputHash) | Medium | Undocumented | Current Rust schema is minimal. |
| Full `manifest.json` schema (rng, provenance section, classes) | Low | Partially done (M-034 added hashes) | Extend existing. |
| `aggregates/` directory | Low | Undocumented | Placeholder in C# today. |
| Series ID format (`{nodeId}@{COMPONENT}@{CLASS}`) | Medium | Undocumented | Current Rust uses bare `{nodeId}`. |

### Gaps (full parity harness)

| Gap | Severity | Status | Notes |
|-----|----------|--------|-------|
| Parity test across all 21 Rust fixtures | High | In-scope (E-20 spec) but incomplete | M-034 tested 3 models. Need coverage of all topology, routing, constraint, PMF fixtures. |
| Casing normalization | Low | Discovered in M-034 | Rust lowercases topology node IDs; C# preserves casing. Parity helper uses case-insensitive matching. |

### Resolution path

Per D-044, work splits into:
1. **Engine core gaps** (per-class, edge series, outputs filtering) → new milestones, prerequisite for E-17/E-18
2. **Artifact sink gaps** (directory layout, metadata, naming) → separate milestones, can follow engine core work
3. **Parity harness** → validates engine core correctness across all fixtures, should land before core gaps to establish baseline

### Blocking relationships

- E-17 (Interactive What-If) requires: per-class decomposition, outputs filtering, full index.json/run.json schema
- E-18 (Time Machine/Pipelines) requires: all of the above + edge series + artifact sink parity
- Svelte UI state display requires: StateQueryService compatibility (spec.yaml with file:// URIs, per-class series)

### Reference

- D-044 (three-layer architecture)
- D-043 (provenance strategy)
- `work/epics/E-20-matrix-engine/spec.md` (original scope)
- `docs/architecture/matrix-engine.md` (future work section)

---
