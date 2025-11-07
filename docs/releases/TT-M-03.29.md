# TT‑M‑03.29 — Service Time (S) Derivation (Processing Time Sum)

**Status:** ✅ Complete  
**Date:** November 6, 2025  
**Branch:** `feature/ui-m-0323-node-detail-panel`

---

## 🎯 Milestone Outcome

Service time is now a first-class metric across the stack. Templates emit `processingTimeMsSum`/`servedCount`, the engine derives `serviceTimeMs` for every service node, the API surfaces it in `/state` and `/state_window`, and the UI exposes both an inspector chart and a "Service Time" color basis with configurable thresholds. Documentation, schemas, and performance tracking describe the new contract, and every gallery template can be demoed without manual edits.

## ✅ Completed Highlights

### Data Contract & Templates
- Added optional `processingTimeMsSum` and `servedCount` semantics to service nodes; schema + DTOs updated accordingly.
- Updated all gallery templates (`supply-chain-*`, incident workflow, IT microservices, manufacturing, network reliability, transportation) to emit the new series; smoke-tested via `FlowTime.Sim.Cli` generation.

### Engine & API
- `StateQueryService` derives `serviceTimeMs` per bin with divide-by-zero guards and adds the series to `/state_window` payloads when available.
- Telemetry warnings now include the new series IDs, and golden responses/regression tests cover the contract.

### UI Experience
- Topology inspector displays a Service Time sparkline + horizon band; Feature Bar can color nodes by Service Time with default thresholds (green ≤ 400 ms, yellow ≤ 700 ms, red > 700 ms).
- Tooltip formatter, canvas focus labels, and sparklines all understand the new basis so service-time information is consistent across the UI.

### Docs & Performance
- Architecture roadmap, milestone doc, and UI M3 roadmap document the contract and thresholds; perf log captures both the debug validation (41 s full suite) and the Release sweep (PMF perf flakes only).
- New `docs/performance/TT-M-03.29-performance-report.md` records the Release run results and outstanding PMF tolerances.

## 📊 Validation

| Command | Outcome |
| --- | --- |
| `dotnet build FlowTime.sln` | ✅ (warnings unchanged) |
| `dotnet test FlowTime.sln` | ✅ (Debug: FlowTime.Tests 190 pass / 1 skip; other projects green) |
| `dotnet test FlowTime.sln -c Release --no-build` | ⚠️ Known PMF performance tolerances triggered (`Test_PMF_Normalization_Performance`, `Test_PMF_Mixed_Workload_Performance`); all other suites green. |

## ⚠️ Known Issues / Follow-Up

- PMF performance thresholds (Normalization/Mixed workload) continue to exceed 20× tolerances in Release builds. This predates TT‑M‑03.29; resolution tracked under the M2 PMF benchmark work.
- Advanced Service Time overlays (node heatmaps, edge overlays) remain scheduled for TT‑M‑03.30.

---

TT‑M‑03.29 is complete—service time derivation is live across templates, engine, API, UI, and documentation.
