# TT‑M‑03.32 — Retry Governance & Terminal Disposition

**Status:** ✅ Complete  
**Date:** December 15, 2025  
**Branch:** `feature/tt-m-0332`

---

## 🎯 Milestone Outcome

Retry governance moved from a design note to a fully supported contract + UI experience. Services can declare `maxAttempts`, runtime stops feeding the retry kernel once items exceed the budget, and `/state`/`/state_window` emit `exhaustedFailures` plus `retryBudgetRemaining`. The topology surface exposes those numbers via chips, inspector rows, and a terminal-edge toggle/badge so operators immediately see when work spills into DLQs/escalations. The canonical supply-chain template demonstrates the full path (Delivery ➜ DLQ ➜ returns triage ➜ restock / secondary / disposal), and new authoring/testing docs capture the workflow for future templates.

## ✅ Completed Highlights

### Contracts & Runtime
- Added `maxAttempts`, `retryBudgetRemaining`, and `exhaustedFailures` to contracts, schema docs, goldens, and runtime loaders (`StateQueryService`, `GraphService`, DTOs, analyzers).
- Invariant analyzer enforces retry governance (non-negative exhausted/budget series, `exhaustedFailures ≤ failures`, warnings when `maxAttempts` lacks supporting telemetry, queue conservation updates).
- Fixtures (`fixtures/time-travel/retry-service-time`) now ship deterministic exhausted/budget CSVs so regression tests cover the new series.

### UI Enhancements
- Feature bar toggles for terminal edges + retry budget, persisted via run-state.
- Topology canvas renders Exhausted/Budget chips, retry badges, and a terminal-edge badge with alias text; inspector rows mirror the metrics.
- `time-travel-state.schema.json`, `Topology.razor`, `TopologyFeatureBar.razor`, and `topologyCanvas.js` updated so the Blazor + JS layers stay in sync.

### Template & Documentation
- `templates/supply-chain-multi-tier.yaml` now models Delivery retries → Rejected DLQ → Returns processing → Restock/Recover/Disposal queues; the example uses `maxDeliveryAttempts`, terminal edges, and new aliases so the UI visuals can be demoed end-to-end.
- Added `docs/templates/template-authoring.md` and `docs/templates/template-testing.md` describing the governance semantics, analyzer workflow, and CLI commands.
- Milestone doc marked complete, tracking file added, follow-up TT‑M‑03.32.1 logged to introduce a dedicated `dlq` node type.

## 📊 Validation

| Command | Outcome |
| --- | --- |
| `dotnet build FlowTime.sln` | ✅ |
| `dotnet test FlowTime.Api.Tests/FlowTime.Api.Tests.csproj` | ✅ |
| `dotnet test FlowTime.sln` | ✅ |

*(Performance suite currently passes; prior flaky PMF ratios did not repro on the final run.)*

## ⚠️ Follow-Up

- **TT‑M‑03.32.1:** introduce a first-class `dlq` node kind (or `queueType: dlq`) so DLQ semantics don’t rely on aliases. That work will add schema/analysis rules and dedicated canvas styling.

---

TT‑M‑03.32 delivers retry budgets, exhausted-flow tracking, and terminal-edge visualization across contracts, runtime, templates, and UI. Operators now have the data and visuals needed to understand retry governance, while the next milestone will polish DLQ-specific semantics.***
