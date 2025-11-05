# TT‑M‑03.28 Implementation Tracking — Retries First‑Class

**Milestone:** TT‑M‑03.28 — Retries (Attempts/Failures, Effort vs Throughput, Temporal Echoes)  
**Started:** TBA  
**Status:** 📋 Not Started  
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
- [ ] Phase 1: Templates + Precompute
- [ ] Phase 2: API Contracts + Tests
- [ ] Phase 3: UI Rendering + Tests
- [ ] Phase 4: Docs + Perf

### Test Status
- Build: ⏳ TBA
- API Tests: ⏳ TBA
- UI Tests: ⏳ TBA
- Full Suite / Perf: ⏳ TBA

---

## Mitigation Tasks (Must‑Do)

- [ ] Kernel governance: validate Σ(kernel) and length; warn/clamp; config caps
- [ ] Artifact‑time precompute of retryEcho for sim; telemetry authoritative
- [ ] Conservation helpers/tests: `attempts = successes + failures`; kernel mass sanity checks
- [ ] Causality enforcement: past‑only references; precompute to avoid cycles
- [ ] Null‑guarding/warnings: consistent unresolved telemetry handling
- [ ] Additive schema/contracts: no breaking changes; doc defaults
- [ ] UI toggles + A11y checks; distinct edge styles; chip layout
- [ ] Rounding/precision normalization for builders/goldens

---

## Phase 1: Templates + Precompute

- [ ] Add retry‑enabled example template with attempts/served/failures and deterministic kernel
- [ ] Implement artifact‑time precompute for retryEcho; bind `file:` URIs

## Phase 2: API Contracts

- [ ] Extend `/graph` with effort vs throughput edges (multiplier/lag)
- [ ] Include attempts/failures/retryEcho in `/state_window`
- [ ] API unit/golden tests for edge types and retry series

## Phase 3: UI Rendering

- [ ] Edge styles for effort vs throughput; multiplier labels (optional)
- [ ] Chips for Attempts/Failures/Retry with toggles and A11y
- [ ] Inspector stack + horizons for retry‑enabled nodes
- [ ] UI tests for inspector/edge styles

## Phase 4: Docs + Perf

- [ ] Update milestone doc + telemetry contract snippet
- [ ] Add perf log entry after full run
- [ ] Roadmap updates for any further deferrals

---

## Issues & Decisions
- [ ] Document kernel policy (default caps, warnings)
- [ ] Confirm fallback policy when retryEcho is not provided (template default vs required)

---

## Final Checklist

### Code Complete
- [ ] All phases complete
- [ ] No compilation errors/warnings beyond baseline

### Documentation
- [ ] Milestone status updated (→ ✅ Complete)
- [ ] Release notes added

### Quality Gates
- [ ] All unit/integration/golden tests passing
- [ ] Performance acceptable (see perf log)
- [ ] No regressions

