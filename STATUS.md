# aiwf status — 2026-05-02

_167 entities · 0 errors · 0 warnings_

## In flight

### E-11 — Svelte UI — Parallel Frontend Track _(active)_

- ✓ **M-061** — Project Scaffold & Shell _(done)_ — ACs 4/4 met
- → **M-062** — Run Orchestration _(in_progress)_ — ACs 0/7 met (7 open)

```mermaid
flowchart LR
  E_11["E-11<br/>Svelte UI — Parallel Frontend Track"]:::epic_active
  M_061["M-061 (4/4)<br/>Project Scaffold & Shell"]:::ms_done
  E_11 --> M_061
  M_062["M-062 (0/7)<br/>Run Orchestration"]:::ms_in_progress
  E_11 --> M_062
  classDef epic_active fill:#d6eaff,stroke:#1a73e8,color:#000
  classDef epic_proposed fill:#f4f4f4,stroke:#888,color:#000
  classDef ms_done fill:#d8f5d8,stroke:#2a8a2a,color:#000
  classDef ms_in_progress fill:#fff3c4,stroke:#caa400,color:#000
  classDef ms_draft fill:#f4f4f4,stroke:#888,color:#000
  classDef ms_cancelled fill:#fbeaea,stroke:#c33,color:#000
```

## Roadmap

### E-13 — Path Analysis & Subgraph Queries _(proposed)_

_(no milestones)_

### E-15 — Telemetry Ingestion, Topology Inference, and Canonical Bundles _(proposed)_

_(no milestones)_

### E-22 — Time Machine — Model Fit & Chunked Evaluation _(proposed)_

_(no milestones)_

## Open decisions

_(none)_

## Open gaps

| ID | Title | Discovered in |
|----|-------|---------------|
| G-001 | Path Analysis / Path Filters |  |
| G-002 | Summary Helpers (Edge/Path Analytics) |  |
| G-003 | Dependency Constraint Enforcement (Deferred M-10.03) |  |
| G-004 | dag-map Layout Quality (Svelte UI) |  |
| G-005 | dag-map Features Needed for Svelte UI M5+ |  |
| G-006 | Svelte UI: SVG Performance at Scale |  |
| G-007 | Client-Side Route Derivation for layoutFlow |  |
| G-008 | Router Convergence Guard (Deferred from Phase 1) |  |
| G-009 | Parallelism \`object?\` Typing (Deferred from Phase 1) |  |
| G-010 | Legacy / Compatibility Surface Cleanup |  |
| G-011 | Continuous Prediction / Crystal Ball Usage Pattern |  |
| G-012 | Streaming Epic Not Formalized |  |
| G-013 | E-18 Model Calibration Needs Crystal Ball Design Input |  |
| G-014 | Deferred deletion: Engine \`POST /v1/run\` and \`POST /v1/graph\` |  |
| G-016 | Rust Engine Parity — Evaluation Core Gaps |  |
| G-017 | E-18 Optimization Constraints (no owner milestone) |  |
| G-018 | \`IModelEvaluator\` Series-Key Shape Divergence |  |
| G-019 | Sim-generated model shape vs. Rust engine compiler expectations |  |
| G-020 | Ultrareview findings on \`epic/E-21-svelte-workbench-and-analysis\` (2026-04-20) |  |
| G-022 | Heatmap view — deferred enhancements (m-E21-06 Q&A, 2026-04-23) |  |
| G-023 | Topology DAG has no keyboard nav or ARIA structure (m-E21-06 AC12 homework) |  |
| G-024 | Data-viz palette not validated for color-blindness (m-E21-06 AC12 homework) |  |
| G-025 | Bidirectional card ↔ view selection (reverse cross-link) |  |
| G-026 | Heatmap sliding-window scrubber (Blazor-parity zoom-and-pan) |  |
| G-032 | \`transportation-basic\` regressed: \`edge_flow_mismatch_incoming\` × 3 after E-24 unification |  |
| G-033 | Tests are too weak: surveyed-output-only canaries cannot detect drift; need deterministic golden-output assertions |  |
| G-034 | Sim orchestration silently demotes dryRun:true when full parameter set is provided | M-062 |

## Warnings

_(none)_

## Recent activity

| Date | Actor | Verb | Detail |
|------|-------|------|--------|
| 2026-05-02 | human/peter | rename | aiwf rename E-18 slug -> headless-pipeline-and-optimization |
| 2026-05-01 | human/peter | import | import(spike): 15 epics + 65 milestones + 53 decisions + 33 gaps |

