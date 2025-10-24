# TT‑M‑03.21 Implementation Tracking — Graph + Metrics endpoints

**Milestone:** TT‑M‑03.21 — Graph + Metrics endpoints (UI Phase 2 enablement)  
**Status:** Planned  
**Branch:** feature/tt-m-0321-graph-metrics  
**Assignee:** Time-Travel Platform

---

## Progress Log

- 2025-10-24 — Drafted milestone and tracker; confirmed contracts from UI roadmap; scheduled small backend scope.

---

## Checklist

### Graph Endpoint
- [ ] Add `GET /v1/runs/{runId}/graph`
- [ ] Parse `model/model.yaml` to nodes/edges/semantics/ui
- [ ] Unit tests: graph extraction
- [ ] Integration tests: network-reliability, microservices
- [ ] Error handling: 404 missing run

### Metrics Endpoint (Minimal)
- [ ] Add `GET /v1/runs/{runId}/metrics?startBin={s}&endBin={e}`
- [ ] Implement SLA% and mini-bars
- [ ] Unit tests: aggregator
- [ ] Integration tests: happy path + invalid range
- [ ] Optional: write `metrics.json` during run creation

### Docs & Samples
- [ ] Update data contracts and UI roadmap references
- [ ] Add `.http` samples

### Validation
- [ ] Golden tests lock response shapes
- [ ] CI runs `dotnet test` suites green

---

## Commands

```bash
# API tests
dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj

# Full
dotnet test --nologo --verbosity=minimal
```

