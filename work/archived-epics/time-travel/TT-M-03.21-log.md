# TT‑M‑03.21 Implementation Tracking — Graph + Metrics endpoints

**Milestone:** TT‑M‑03.21 — Graph + Metrics endpoints (UI Phase 2 enablement)  
**Status:** Completed  
**Branch:** feature/time-travel-ui-m3  
**Assignee:** Time-Travel Platform

---

## Progress Log

- 2025-10-24 — Drafted milestone and tracker; confirmed contracts from UI roadmap; scheduled small backend scope.
- 2025-10-24 — Implemented graph + metrics services, wired endpoints, added integration coverage, refreshed docs and samples.

---

## Checklist

### Graph Endpoint
- [x] Add `GET /v1/runs/{runId}/graph`
- [x] Parse `model/model.yaml` to nodes/edges/semantics/ui
- [x] Unit tests: graph extraction
- [x] Integration tests: network-reliability, microservices
- [x] Error handling: 404 missing run

### Metrics Endpoint (Minimal)
- [x] Add `GET /v1/runs/{runId}/metrics?startBin={s}&endBin={e}`
- [x] Implement SLA% and mini-bars
- [x] Unit tests: aggregator
- [x] Integration tests: happy path + invalid range
- [x] Optional: write `metrics.json` during run creation

### Docs & Samples
- [x] Update data contracts and UI roadmap references
- [x] Add `.http` samples

### Validation
- [x] Golden tests lock response shapes
- [x] CI runs `dotnet test` suites green

---

## Commands

```bash
# API tests
dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj

# Full
dotnet test --nologo --verbosity=minimal
```
