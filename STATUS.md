# aiwf status — 2026-05-04

_173 entities · 0 errors · 0 warnings_

## In flight

_(no active epics)_

## Roadmap

### E-13 — Path Analysis & Subgraph Queries _(proposed)_

_(no milestones)_

### E-15 — Telemetry Ingestion, Topology Inference, and Canonical Bundles _(proposed)_

_(no milestones)_

### E-22 — Time Machine — Model Fit & Chunked Evaluation _(proposed)_

_(no milestones)_

### E-25 — Engine Truth Gate — Edge-Flow Authority + Golden-Output Canary _(proposed)_

- **M-066** — Edge-Flow Authority Decision _(draft)_ — ACs 0/9 met (9 open)
- **M-067** — Engine + Template Alignment _(draft)_ — ACs 0/13 met (13 open)
- **M-068** — Golden-Output Canary _(draft)_ — ACs 0/14 met (14 open)

```mermaid
flowchart LR
  E_25["E-25<br/>Engine Truth Gate — Edge-Flow Authority + Golden-Output Canary"]:::epic_proposed
  M_066["M-066 (0/9)<br/>Edge-Flow Authority Decision"]:::ms_draft
  E_25 --> M_066
  M_067["M-067 (0/13)<br/>Engine + Template Alignment"]:::ms_draft
  E_25 --> M_067
  M_068["M-068 (0/14)<br/>Golden-Output Canary"]:::ms_draft
  E_25 --> M_068
  classDef epic_active fill:#d6eaff,stroke:#1a73e8,color:#000
  classDef epic_proposed fill:#f4f4f4,stroke:#888,color:#000
  classDef ms_done fill:#d8f5d8,stroke:#2a8a2a,color:#000
  classDef ms_in_progress fill:#fff3c4,stroke:#caa400,color:#000
  classDef ms_draft fill:#f4f4f4,stroke:#888,color:#000
  classDef ms_cancelled fill:#fbeaea,stroke:#c33,color:#000
```

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
| G-035 | Promote InvariantAnalyzer warnings to CI gate |  |
| G-036 | Refresh docs/development/versioning.md to current FlowTime versioning state |  |

## Warnings

_(none)_

## Recent activity

| Date | Actor | Verb | Detail |
|------|-------|------|--------|
| 2026-05-03 | human/peter | add | aiwf add gap G-036 'Refresh docs/development/versioning.md to current FlowTime versioning state' |
| 2026-05-03 | human/peter | add | aiwf add gap G-035 'Promote InvariantAnalyzer warnings to CI gate' |
| 2026-05-02 | human/peter | render-roadmap | aiwf render roadmap |
| 2026-05-02 | human/peter | add | aiwf add milestone M-068 'Golden-Output Canary' |
| 2026-05-02 | human/peter | add | aiwf add milestone M-067 'Engine + Template Alignment' |

