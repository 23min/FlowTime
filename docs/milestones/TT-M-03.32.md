# TT‑M‑03.32 — Retry Governance & Terminal Disposition

Status: ✅ Complete  
Owners: Platform (API/Runtime) + UI

## Overview

Layer governance semantics on top of the retry foundations delivered in TT‑M‑03.28/30/31. Introduce max-attempt budgets, exhausted failure tracking, and explicit terminal edges so models can answer “how many retries do we allow?”, “what happens when budgets are exhausted?”, and “where does unrecoverable work go?”. Provide operator-facing visuals (budget gauges, terminal edge styling) and contract updates across templates, API, and UI.

## Motivation & Benefits

1. **Retry Budgets as a First-Class Control**
   - Templates must be able to declare `maxAttempts` per service. Runtime needs to stop feeding the retry kernel once an item exceeds the budget and emit `exhaustedFailures` so operators see how much work permanently fails.
   - Benefits: deterministic retry pressure, support for compliance/SLOs, clear signals when budgets are hit.

2. **Terminal Disposition & Flow Accounting**
   - Exhausted items must be routed explicitly via `terminal` edges (e.g., DLQ, escalation, scrap). This keeps flow conservation intact and lets downstream nodes model backlog/latency for unrecoverable work.
   - Domain neutral: “terminal node” covers DLQ (IT ops), manual triage (support), scrap bin (manufacturing), drop bucket (network).

3. **Operator Visibility**
   - UI needs chips/inspector blocks for Exhausted, Budget Remaining, and terminal queues. Canvas requires terminal edge styling (distinct stroke/badge) plus feature-bar toggles.
   - Benefits: quick diagnosis of runaway retries, DLQ backlogs, or missing escalation capacity.

## Scope

### In Scope
- Schema/API: `maxAttempts`, `exhaustedFailures`, `retryBudgetRemaining`, terminal `edgeType`, telemetry warnings.
- Runtime: track per-item attempt counts, stop retry kernel contributions post-budget, emit exhausted series, and route exhausted flows via terminal edges.
- UI: render governance metrics in chips/inspector, add terminal-edge styling and toggles, surface budget gauges.
- Docs: update architecture/operator guides and add demo checklist.

### Out of Scope
- Automated policy enforcement (alerts).  
- Adaptive retry kernels (still manual).  
- Non-retry failure handling (e.g., general DLQ ingestion pipelines).

## Deliverables
1. Schema/contract updates documenting new fields and terminal edges.
2. Engine/runtime support for budgets/exhausted flows.
3. UI surfaces (chips, terminal edges, toggles) + tests.
4. Docs: milestone summary, architecture update, operator how-to.

## Dependencies
- Built atop TT‑M‑03.31 server edge slice; no other blocking dependencies.
- Coordinate with template owners to add `maxAttempts` where relevant.

## Next Steps
- Finalize spec for schema/API changes (fields, telemetry).
- Prototype runtime enforcement with a deterministic fixture.
- Update UI canvas/inspector with new metrics and styling.

## Implementation Notes
- **Runtime/API:** `/state` and `/state_window` now emit `maxAttempts`, `retryBudgetRemaining`, and `exhaustedFailures` plus terminal edge metadata. See `StateQueryService`, `docs/schemas/time-travel-state.schema.json`, and the updated golden fixtures under `tests/FlowTime.Api.Tests/Golden/`.
- **UI:** Feature bar exposes retry-budget and terminal-edge toggles. Canvas renders exhausted/budget chips and badges, while inspector blocks/rows respond to the new settings. Relevant files include `Topology.razor`, `TopologyFeatureBar.razor`, and `wwwroot/js/topologyCanvas.js`.
- **Fixtures/Tests:** `fixtures/time-travel/retry-service-time` gained deterministic exhausted/budget series so API/UI tests cover the governance workflow. State/graph goldens and UI tests were refreshed to account for the new metrics and overlay payload fields.
- **Follow-Up:** TT‑M‑03.32.1 tracks the dedicated `dlq` node type so future templates can differentiate DLQs without relying on aliases.
