# FT-M-05.13.1 — Class Filter Dimming Gap (Transportation Classes)

**Status:** 📋 Planned  
**Dependencies:** ✅ FT-M-05.13  
**Target:** Ensure class filtering highlights Airport class traffic on LineAirport and Airport nodes in the class-segmented transportation template.

---

## Overview

When filtering the Transportation Network with Hub Queue (Class Segments) template by class (e.g., Airport Express), the LineAirport and Airport nodes remain dimmed even though they should carry Airport flow. This is caused by derived series that do not emit class-specific data, leaving the UI with no class metrics to evaluate.

This milestone closes that gap by making class filtering consistent with routed class semantics for derived airport legs.

---

## Scope

### In Scope ✅
1. Identify the class-series coverage for derived airport legs in the class template.
2. Ensure class filtering uses class-scoped data for LineAirport and Airport nodes.
3. Add tests that assert Airport class selection lights up LineAirport and Airport.

### Out of Scope ❌
- Reworking class routing logic beyond the affected derived series.
- UI redesign of class selector or focus view.

---

## Requirements

### FR1: Class-aware derived series
**Description:** Derived airport leg series must be class-visible when class routing guarantees exclusivity.

**Acceptance Criteria**
- [ ] LineAirport and Airport nodes report non-zero class metrics for Airport class in class-segmented runs.
- [ ] No regression for Downtown/Industrial classes.

### FR2: UI class filter correctness
**Description:** Class filter should not dim nodes when class data is present for selected class.

**Acceptance Criteria**
- [ ] Airport class selection keeps LineAirport and Airport nodes highlighted.
- [ ] Dimming remains for nodes with no class data.

---

## Implementation Plan

### Phase 1 — Diagnose + Test Harness
1. Add a failing UI test that captures the dimming gap.
2. Identify whether fix should be in template series emission or UI class filtering logic.

### Phase 2 — Fix + Validation
1. Implement class series emission (or equivalent logic) for derived airport legs.
2. Update any template docs or schema references as needed.

### Phase 3 — Regression Coverage
1. Add tests for class filters across Airport, Downtown, Industrial.
2. Run full UI test suite.

---

## Test Plan

### UI Tests
- `Topology_ClassFilter_AirportHighlightsLineAirport`
- `Topology_ClassFilter_AirportHighlightsAirport`

---

## Success Criteria

- Airport class filter no longer dims LineAirport and Airport nodes.
- All UI tests pass (excluding approved perf skips).
