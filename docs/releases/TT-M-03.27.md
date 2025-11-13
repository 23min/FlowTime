# TT-M-03.27 — Queues First-Class (Backlog + Latency; No Retries)

**Status:** ✅ Complete  
**Date:** November 4, 2025  
**Branch:** `feature/tt-m-03-27/queues-first-class`

---

## 🎯 Milestone Outcome

Queues are now first-class citizens in the Time-Travel stack. The warehouse 1d/5m template includes an explicit `DistributorQueue` node; queue depth is precomputed at artifact generation (SHIFT-based recurrence with initial condition) and bound to concrete CSV files; `/state_window` delivers `latencyMinutes` derived via Little’s Law (null-safe when `served == 0`); and the UI renders queue nodes with badge toggles and inspector horizons for Queue/Latency/Arrivals/Served. Documentation, roadmap deferrals, and performance logs are updated to capture the new behaviours and remaining follow-ups.

## ✅ Completed Highlights

### Templates & Artifact Generation
- Inserted `DistributorQueue` between Warehouse and Distributor with `queue_inflow`, `queue_outflow`, and `queue_depth` series.
- `RunArtifactWriter` precomputes `queue_depth = MAX(0, q(t-1) + inflow - outflow)` using topology `initialCondition.queueDepth`, emitting CSVs and normalising `semantics.queueDepth` to `file:` URIs.

### API Enhancements
- `/state_window` builder emits `latencyMinutes = (queue/served) * binMinutes` for queue nodes, null when `served == 0`.
- Added targeted test + golden snapshot (`state-window-queue-null-approved.json`) to cover the zero-served case.

### UI Experience
- Canvas recognises `kind=queue` rectangles, supports a persisted queue depth badge toggle, and avoids chip overlap (queue chip left of errors on queue nodes).
- Inspector renders four queue series with horizon overlays; new unit test guarantees Queue/Latency/Arrivals/Served coverage.

### Docs & Tracking
- Architecture note: `docs/architecture/time-travel/queues-shift-depth-and-initial-conditions.md` (SHIFT, initial conditions, telemetry guidance).
- Milestone/tracking docs updated; roadmap deferrals capture retries, service-time S, oldest_age, edge overlays, and API fallback depth.
- Per-milestone performance log consolidated in `docs/performance/perf-log.md` (full-suite run recorded).

## 📊 Validation

| Command | Outcome |
| --- | --- |
| `dotnet build FlowTime.sln -c Release` | ✅ |
| `dotnet test tests/FlowTime.Api.Tests -c Release --no-build` | ✅ |
| `dotnet test tests/FlowTime.UI.Tests -c Release --no-build` | ✅ |
| `dotnet test tests/FlowTime.Tests -c Release --no-build` | ✅ (190 pass / 1 skip) |

Manual confirmation: warehouse 1d/5m template now produces queue depth CSV, canvas badge toggle works, inspector horizons align with `/state_window` payloads (latency null for zero-served bin).

## 📌 Deferred / Follow-Up

- API fallback to reconstruct queue depth when telemetry omits it (documented in roadmap deferred list).
- Retries/backoff modelling, service time S metrics, oldest-age telemetry, and edge overlays remain out of scope for TT‑M‑03.27.
- Release notes to integrate this summary into the broader TT release once scheduled.

---

TT‑M‑03.27 is complete — queues are now first-class in FlowTime Time-Travel.
