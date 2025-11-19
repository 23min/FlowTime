# TT‑M‑03.32 — Retry Governance & Terminal Disposition (Tracking)

**Started:** 2025-12-05  
**Status:** ✅ Complete  
**Branch:** `feature/tt-m-0332`  
**Assignee:** Platform + UI Team (shared)

---

## Summary

- Schema/DTOs: `maxAttempts`, `exhaustedFailures`, `retryBudgetRemaining`, and terminal `edgeType` are now part of `/state`, `/state_window`, and `/graph`.
- Runtime/UI: `StateQueryService`, `GraphService`, topology canvas, feature bar, and inspector chips all surface the new metrics plus a terminal-edge toggle/badge.
- Templates/Docs: `templates/supply-chain-multi-tier.yaml` models Delivery → DLQ → returns/restock/recover/scrap. Deterministic fixtures + goldens updated. Authoring/testing guides added under `docs/templates/`.
- Tests: `dotnet build FlowTime.sln` and `dotnet test FlowTime.sln` run clean (performance suite passes). API/UI golden suites refreshed.

Follow-up work for TT‑M‑03.32.1 will introduce a dedicated `dlq` node type so DLQ semantics are first-class rather than alias-based.

---

## Validation Checklist

| Check | Status |
| --- | --- |
| dotnet build FlowTime.sln | ✅ |
| dotnet test FlowTime.Api.Tests/FlowTime.Api.Tests.csproj | ✅ |
| dotnet test FlowTime.sln | ✅ |
| Supply-chain template exercising exhausted/budget series | ✅ |

---

## Final Notes

- Milestone doc updated to ✅ Complete.
- Release notes captured in `docs/releases/TT-M-03.32.md`.
- No open blockers; next steps tracked in TT‑M‑03.32.1.
