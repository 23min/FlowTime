# TT-M-03.20 — Typed Array Parameters (Templates ➜ Engine)

**Status:** 📋 Proposed  
**Dependencies:** ✅ TT‑M‑03.19 (Seeded RNG & Provenance)  
**Owners:** Time‑Travel Platform / Template Service  
**Target Outcome:** Add first‑class, strongly‑typed array parameter support to the template service and engine conversion so templates can safely express array inputs (e.g., const series) without brittle string substitution. Provide validation (type/length/range), keep YAML substitution deterministic, and prevent runtime deserialization failures.

---

## Overview

Some existing templates (e.g., `it-system-microservices`) embed array literals via parameter substitution for `const` nodes. This works incidentally because the substituted text happens to parse into a double[] later. Other templates (e.g., `network-reliability`) expose more complex shapes and hit an edge case where loose substitution leaves an unexpected string that cannot be deserialized into typed series, causing 500s.

This milestone formalizes array parameters end‑to‑end:
- Parse `type: array` parameters into typed arrays (e.g., double[]).
- Preserve types through model generation, not just raw text substitution.
- Validate length and value constraints (optionally against the grid where applicable).
- Provide clear validation errors instead of runtime exceptions.

---

## Goals

- First‑class `array` parameter type with element typing (v1: `double`, `int`).
- Deterministic conversion: parameters become typed arrays before engine parsing.
- Validation:
  - Type checks (all elements parse to the declared type).
  - Min/Max value checks (if defined) per element.
  - Optional length matching to `grid.bins` for const series.
- Backwards compatibility: inline array literals continue to work.
- Clear errors: template/parameter validation fails fast with actionable messages.

### Non‑Goals

- Expression‑level array operations (beyond current engine scope).
- Auto‑broadcasting/scalar expansion (future consideration).
- Complex nested arrays or mixed‑type arrays.

---

## Scope

1. Parameter model & parsing
   - Add element type hint to array params (e.g., `arrayOf: double`), default `double` when omitted.
   - Parse into typed arrays prior to YAML substitution.

2. TemplateService substitutions
   - Update substitution to avoid lossy stringification for arrays.
   - Bind parsed arrays directly into the generated model for `const` nodes (not as raw text).

3. Validation
   - Extend validator to enforce array typing, min/max per element, and (when applicable) length == `grid.bins` for const time‑series.
   - Return clear error messages (parameter name, reason).

4. Engine/Writer
   - Ensure only typed arrays reach artifact writer; remove string fallbacks for const series.

5. Tests & Docs
   - Unit tests: parameter parsing, substitution, validation.
   - Integration tests: `network-reliability` default + overrides; length mismatch error.
   - Docs: update template schema guidance to include `arrayOf` and examples.

---

## Functional Requirements

1. FR1 — Parameter typing
   - `type: array` with optional `arrayOf: double|int` (default `double`).
   - UI/JSON input forms accepted; mixed/invalid items cause validation errors.

2. FR2 — Substitution semantics
   - Arrays are not injected as raw strings; the model generation binds typed arrays for `const` nodes.

3. FR3 — Validation
   - Elements conform to declared type.
   - Min/Max (if present) apply per element.
   - For const time‑series, array length equals `grid.bins` (no silent tiling in v1).

4. FR4 — Errors
   - `/v1/runs` responds 400 with actionable messages.

### Non‑Functional Requirements
- NFR1 — Determinism: same inputs → same typed arrays.
- NFR2 — Testability: targeted unit/integration tests.
- NFR3 — Back‑compat: raw literal arrays still work.

---

## Implementation Plan

1) Contracts/Schema
- Document `arrayOf` in schema guidance (docs only); code reads hint from YAML.

2) TemplateService
- Parse array parameters into typed arrays.
- Update substitution pipeline to keep arrays typed and bind to `const` nodes.

3) Validation
- Enforce element type, min/max, and optional length checks vs `grid.bins` for full‑series const nodes.

4) Engine
- Confirm writer only sees typed arrays for const nodes; remove string paths.

5) Tests
- RED ➜ GREEN: parsing, validation, integration with `network-reliability`.

6) Docs
- Add guidance and examples under `docs/schemas/template-schema.md` and link from `docs/schemas/README.md`.

---

## Deliverables

- Typed array parameter parsing and validation.
- Updated model generation for const series.
- Integration tests for success and error scenarios.
- Documentation with examples.

---

## Test Plan

Automated
- Parameter parsing unit tests (arrayOf: double/int; invalid/mixed types).
- Validator tests (min/max violations; length mismatch vs grid).
- `/v1/runs` integration tests (success and 400 with clear error text).

Manual
1) Run `network-reliability` with defaults → succeeds.
2) Override `baseLoad` with a matching array → succeeds; outputs change accordingly.
3) Provide a shorter array while `bins` is larger → 400 with “length mismatch” message.

---

## Acceptance Criteria

- Array parameters become typed arrays in the generated model; no raw string substitution for const series.
- Invalid array inputs (type/length/min/max) yield 400 with actionable messages.
- `network-reliability` runs without deserialization errors using parameterized arrays.
- All tests remain green.

---

## Risks & Mitigations

- Back‑compat for templates that rely on raw array text: preserve literal handling while preferring typed binding.
- When to enforce length == bins: start with explicit const‑series nodes and avoid guessing for expression‑fed arrays.
- UI ergonomics: accept JSON arrays and document examples clearly.

---

## References

- docs/milestones/TT-M-03.19.md
- docs/schemas/template.schema.json
- docs/schemas/template-schema.md
- templates/network-reliability.yaml

