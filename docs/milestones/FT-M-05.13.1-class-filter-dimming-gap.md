# FT-M-05.13.1 — Class Filter Dimming Gap (Transportation Classes)

**Status:** 📋 Planned  
**Dependencies:** ✅ FT-M-05.13  
**Target:** Ensure class filtering highlights Airport class traffic on LineAirport and Airport nodes by fixing class propagation and adding analyzer validation.

---

## Overview

When filtering the Transportation Network with Hub Queue (Class Segments) template by class (e.g., Airport Express), the LineAirport and Airport nodes remain dimmed even though they should carry Airport flow. The root cause is missing class propagation for topology service-with-buffer semantics, so derived series (served/arrivals for airport legs) never get class series.

This milestone closes that gap by propagating class series where the topology semantics imply it, and by adding analyzer warnings when class propagation is incomplete.

---

## Scope

### In Scope ✅
1. Add class propagation for topology `serviceWithBuffer` semantics (served/errors) when arrivals are classed.
2. Emit analyzer warnings when topology nodes have classed arrivals but missing served/errors class series.
3. Add non-template-specific tests in core analyzer/aggregation layers.

### Out of Scope ❌
- Reworking class routing logic beyond the affected derived series.
- UI redesign of class selector or focus view.

---

## Requirements

### FR1: Topology-based class propagation
**Description:** Topology `serviceWithBuffer` semantics must propagate class series from arrivals to served/errors when class series are missing.

**Acceptance Criteria**
- [ ] Served/errors series referenced by service-with-buffer topology nodes include class series when arrivals are classed.
- [ ] Derived airport leg series become class-visible after propagation.

### FR2: Analyzer coverage warning
**Description:** Invariant analysis warns when topology nodes have classed arrivals but missing served/errors class series.

**Acceptance Criteria**
- [ ] Analyzer emits warnings with consistent codes for missing/partial class coverage.
- [ ] No warnings emitted when class propagation is complete.

---

## Implementation Plan

### Phase 1 — Diagnose + Tests (Core)
1. Add failing core tests for topology class propagation and analyzer warnings.
2. Identify coverage gaps in class propagation path.

### Phase 2 — Fix + Validation
1. Implement topology-based class propagation in class contribution builder.
2. Add analyzer warning logic for topology class coverage gaps.

### Phase 3 — Regression Coverage
1. Add core regression tests for multiple classes.
2. Run full test suite.

---

## Test Plan

### Core Tests
- `ClassContributionBuilder_PropagatesServiceWithBufferTopologyClasses`
- `InvariantAnalyzer_WarnsOnTopologyClassCoverageGaps`

---

## Success Criteria

- Airport class filter no longer dims LineAirport and Airport nodes.
- All UI tests pass (excluding approved perf skips).
