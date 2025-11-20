# TT‑M‑03.32.1 — First-Class DLQ Nodes (Tracking)

**Started:** 2025-12-19  
**Status:** 🚧 In Progress  
**Branch:** `feature/tt-m-03321`  
**Assignee:** Platform + UI + Template Guild

---

## Summary

- Core platform/UI work landed: `kind: dlq` flows end-to-end (schema → analyzer → API → UI) and the supply-chain multi-tier template now exercises the new semantics.
- Remaining scope is template retrofits: make every canonical queue behave realistically (backlog-aware outflows, attrition) and surface exhausted flows through DLQs/terminal sinks.
- This tracking doc ties the retrofit plan to concrete tasks so we can update docs/fixtures and decide whether both supply-chain templates remain necessary or can be consolidated.

---

## Template Retrofit Checklist

| Template | DLQ/Terminal Plan | Status | Notes |
| --- | --- | --- | --- |
| `templates/it-system-microservices.yaml` | Add backlog-aware ingress queue, DLQ fed by `db_retry_failures`, and terminal queue for dropped sessions. | ⏳ Planned | Need telemetry fixture update + docs. |
| `templates/manufacturing-line.yaml` | Fix WIP queue math and route QC scrap to DLQ/terminal nodes. | ⏳ Planned | Consider scrap vs. supplier dispute DLQs. |
| `templates/network-reliability.yaml` | Model realistic request backlog, DLQ for retry/database failures, packet attrition terminal queue. | ⏳ Planned | Requires new analyzer fixture. |
| `templates/supply-chain-incident-retry.yaml` | Backlog-aware incident queue, DLQs for escalation/abandonment. | ⏳ Planned | Evaluate overlap with multi-tier template. |
| `templates/transportation-basic.yaml` | Improve hub queue dynamics, add DLQ/terminal sinks for stranded passengers/unmet demand. | ⏳ Planned | Coordinate with transit telemetry docs. |
| `templates/supply-chain-multi-tier.yaml` | Tune distribution queue backlog + attrition; validate existing DLQ behavior. | ⏳ Planned | Already partially migrated; finish backlog math + docs. |

Additional actions:
- Refresh all golden fixtures/tests once template changes land.
- Update `docs/templates/template-authoring.md` and `template-testing.md` with template-specific DLQ guidance/screens.
- Re-run SIM + Engine analyzers on each template to capture warnings/regressions.
- Evaluate consolidation of the two supply-chain templates (multi-tier vs. incident retry) after both are DLQ-complete; track decision in release notes.

---

## Validation Checklist (per retrofitted template)

For each template above, ensure:

1. `flow-sim generate --id <template>` succeeds with defaults.
2. Analyzer output free of unexpected DLQ/queue warnings (only the intentional “served == 0” info for true terminals).
3. `dotnet test FlowTime.Api.Tests/FlowTime.Api.Tests.csproj` (goldens refreshed).
4. `dotnet test FlowTime.sln` (document known perf guard failures if they persist).

---

## Notes / Decisions

- The organization currently ships two supply-chain templates (`supply-chain-multi-tier` and `supply-chain-incident-retry`). After both are updated we will revisit whether we need both (multi-tier emphasizes logistics/restock DLQs, incident retry models IT ticket escalations). Decision TBD; capture outcome in this tracker and the milestone doc.
