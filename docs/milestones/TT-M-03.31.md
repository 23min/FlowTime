# TT‑M‑03.31 — End‑to‑End Fixtures, Goldens, and Documentation (Retries + Service Time)

Status: Planned  
Owners: Platform (API) + UI  
References: docs/development/milestone-documentation-guide.md, docs/development/TEMPLATE-tracking.md

## Overview

Close out the “Retry + Service Time” epic with production‑quality fixtures, golden tests for API contracts, UI tests, and documentation. Ensure examples clearly show how retries impact dependencies and how service time affects node coloring.

## Goals

- Provide reproducible fixtures/templates that exercise retries and processing time.  
- Lock API contracts via golden responses.  
- Update operator and architecture docs (contracts, thresholds, examples).  
- Wrap with a validation checklist and a how‑to for demo scenarios.

## Scope

In Scope
- Fixtures: microservices example with tunable retry probability and processing times.  
- API: golden snapshots for `/state` and `/state_window` including `edges` and `serviceTimeMs`.  
- UI: unit/integration tests for overlays, basis switching, and inspector series.

Out of Scope
- Performance benchmarks beyond existing targets (track regressions only).

## Deliverables
1) Example fixture bundle(s) under `fixtures/` with README.  
2) Golden tests for API contracts (state and window).  
3) UI tests for overlays + S basis + inspector retries.  
4) Docs: milestone updates, roadmap references, contract tables, operator how‑to.

## Acceptance Criteria
- AC1: `dotnet test` passes all new goldens/UI tests (outside known unrelated perf skips).  
- AC2: Example can be generated and viewed in the UI; overlays/basis switch work.  
- AC3: Docs present clear examples and thresholds with a demo script.

## Implementation Plan (Sessions)

Session 1 — Fixtures + README  
- Finalize example series; add README and CLI to generate artifacts.

Session 2 — Golden Tests  
- Add snapshots pinning `/state` and `/state_window` shapes and key values.  
- Include edge retry series and node service time fields.

Session 3 — UI Tests  
- Toggle overlays; verify legend; verify S basis; hover/selection link tests.  
- Basic snapshot of inspector retries.

Session 4 — Docs + Sign‑off  
- Architecture contracts; operator guide; screenshots; finalize milestone status.

## Risks & Mitigations
- Flaky goldens due to RNG → seed deterministically; document seeds.  
- Payload bloat → slice windows; avoid full‑range defaults in tests.

## References
- docs/architecture/time-travel/time-travel-architecture-ch2-data-contracts.md  
- docs/architecture/time-travel/ui-m3-roadmap.md  
- docs/operations/telemetry-capture-guide.md  
- docs/development/milestone-documentation-guide.md

