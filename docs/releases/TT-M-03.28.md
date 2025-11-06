# TT‑M‑03.28 — Retries First-Class (Attempts/Failures, Effort vs Throughput, Temporal Echoes)

**Status:** ✅ Complete  
**Date:** November 7, 2025  
**Branch:** `feature/ui-m-0323-node-detail-panel`

---

## 🎯 Milestone Outcome

Retries are now modeled as first-class workload: templates expose attempts/served/failures, effort edges distinguish dependency load from throughput, and retry-induced echoes are simulated via causal kernels. The API delivers the new series, the UI renders chips/inspector stacks with distinct edge styling, and documentation/perf tracking capture the new contract.

## ✅ Completed Highlights

### Templates & Simulation
- Added the `Incident Workflow With Retries (IT Ops)` template with deterministic `[0.0, 0.6, 0.3, 0.1]` retry kernel and both throughput/effort edges.
- Artifact writer precomputes `retryEcho` for simulation runs when telemetry omits it; kernel policy enforces max length/mass and warns on clamps.

### API Enhancements
- `/v1/runs/{id}/graph` surfaces `edgeType` distinctions (`throughput`, `effort`) plus `multiplier` and `lag`.
- `/v1/runs/{id}/state_window` returns attempts, failures, retryEcho (derived when absent) with conservation/series-missing warnings captured as structured telemetry (`attempts_series_missing`, `retry_echo_series_missing`, etc.).
- Added rounding normalisation to stabilise floating precision and conservation validation warnings if `attempts ≠ served + failures`.

### UI Experience
- Canvas draws throughput vs effort edges with unique strokes; optional multiplier labels gated by feature toggle.
- Node chips include Attempts, Failures, Retry; inspector stacks render Attempts/Failures/Retry Echo/Served with horizon overlays.
- Feature bar gained toggles for retry metrics and multiplier overlays; tests cover payload propagation and chip visibility.

### Docs & Tracking
- `docs/architecture/retry-modeling.md` now documents kernel governance, retry echo derivation, terminal governance roadmap, and telemetry warnings.
- Milestone and tracking docs updated; roadmap defers max-attempt governance to TT‑M‑03.30 and terminology mapping to TT‑M‑03.30.1.
- Perf log updated with the full-suite regression run.

## 📊 Validation

| Command | Outcome |
| --- | --- |
| `dotnet build FlowTime.sln -c Release` | ✅ |
| `dotnet test tests/FlowTime.Api.Tests -c Release --no-build` | ✅ (150 pass) |
| `dotnet test tests/FlowTime.UI.Tests -c Release --no-build` | ✅ (136 pass) |
| `dotnet test FlowTime.Tests -c Release --no-build` | ✅ (full suite) |

Manual verification: Incident retry template chips now render numeric attempts/failures/retry echo; effort edges display dashed crimson styling with multiplier labels; inspector exposes retry stack with horizons; `/state_window` conservation holds across bins.

## 📌 Deferred / Follow-Up

- Max-attempt governance + terminal edges (TT‑M‑03.30).
- Domain terminology alias mapping for template metrics (TT‑M‑03.30.1).
- Advanced edge overlays and service-time S metrics remain out of scope.

---

TT‑M‑03.28 is complete — retries are now a first-class modeling primitive across templates, API, and UI.***
