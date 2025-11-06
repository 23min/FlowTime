# TT‑M‑03.28 Implementation Tracking — Retries First‑Class

**Milestone:** TT‑M‑03.28 — Retries (Attempts/Failures, Effort vs Throughput, Temporal Echoes)  
**Started:** 2025-03-18  
**Status:** 🚧 In Progress (roadmap follow-up pending)  
**Branch:** `feature/tt-m-03-28/retries-first-class`

---

## Quick Links
- Milestone: `docs/milestones/TT-M-03.28.md`
- Architecture: `docs/architecture/retry-modeling.md`
- Queue/SHIFT reference: `docs/architecture/time-travel/queues-shift-depth-and-initial-conditions.md`
- Perf log: `docs/performance/perf-log.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Templates + Operators
- [x] Phase 2: API Contracts + Tests
- [x] Phase 3: UI Rendering + Tests
- [x] Phase 4: Docs + Perf (roadmap/deferrals captured)

### Test Status
- Build: ✅ `dotnet build FlowTime.sln -c Release`
- API Tests: ✅ `dotnet test tests/FlowTime.Api.Tests -c Release --no-build`
- UI Tests: ✅ `dotnet test tests/FlowTime.UI.Tests -c Release --no-build`
- Full Suite / Perf: ✅ `dotnet test tests/FlowTime.Tests -c Release --no-build` (~6m33s; 1 skip)

---

## Mitigation Tasks (Must‑Do)

- [x] Kernel governance: validate Σ(kernel) and length; warn/clamp; config caps
- [x] Artifact-time precompute of retryEcho for sim; telemetry authoritative
- [x] Conservation helpers/tests: `attempts = successes + failures`; kernel mass sanity checks
- [x] Causality enforcement: past-only references; precompute to avoid cycles
- [x] Null-guarding/warnings: consistent unresolved telemetry handling
- [x] Additive schema/contracts: no breaking changes; doc defaults
- [x] UI toggles + A11y checks; distinct edge styles; chip layout
- [x] Rounding/precision normalization for builders/goldens

---

## Phase 1: Templates + Operators

- [x] Add retry-enabled example template with attempts/served/failures and deterministic kernel
- [x] Implement artifact-time precompute for retryEcho; bind `file:` URIs

## Phase 2: API Contracts

- [x] Extend `/graph` with effort vs throughput edges (multiplier/lag)
- [x] Include attempts/failures/retryEcho in `/state_window`
- [x] API unit/golden tests for edge types and retry series

## Phase 3: UI Rendering

- [x] Edge styles for effort vs throughput; multiplier labels (optional)
- [x] Chips for Attempts/Failures/Retry with toggles and A11y
- [x] Inspector stack + horizons for retry-enabled nodes
- [x] UI tests for inspector/edge styles

## Phase 4: Docs + Perf

- [x] Update milestone doc + telemetry contract snippet
- [x] Add perf log entry after full run
- [x] Roadmap updates for any further deferrals

---

## Issues & Decisions
- [x] Document kernel policy (default caps, warnings)
- [x] Confirm fallback policy when retryEcho is not provided (template default vs required)
- [ ] Domain terminology mapping deferred to TT‑M‑03.30.1 (aliases for template-specific labels)
- [x] Gap: telemetry replay controls only exist on legacy /artifacts route; expose capture/replay actions in the RunDetailsDrawer for parity.
- Note: RetryEcho currently derived in API when kernel present; artifact-time precompute still pending for simulation path.

---

## Final Checklist

### Code Complete
- [x] All phases complete
- [x] No compilation errors/warnings beyond baseline

### Documentation
- [x] Milestone status updated (→ ✅ Complete)
- [x] Release notes added

### Quality Gates
- [x] All unit/integration/golden tests passing
- [x] Performance acceptable (see perf log)
- [x] No regressions
