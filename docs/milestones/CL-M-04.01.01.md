# CL-M-04.01.01 — Template Schema Alignment & Validation

**Status:** 📋 Planned  
**Dependencies:** ✅ CL-M-04.01  
**Target:** Produce an authoritative template schema (JSON + docs) that matches Sim’s class-aware template model and wire schema-backed validation into the Sim toolchain/CLI to prevent drift.

---

## Overview

The class-aware template model shipped in CL-M-04.01 is enforced by C# validators and model classes, but the reference template schema files remain legacy. This follow-on aligns the documented template schema with the runtime model, adds schema-backed validation to the Sim pipeline, and updates CLI/docs to keep authors and tooling in sync.

### Strategic Context
- **Motivation:** Avoid divergence between documented template shape and the runtime validator; enable schema-driven tooling/help text.
- **Impact:** Template authors get accurate schema docs and early schema validation errors; CLI can surface schema-driven diagnostics; future UI/editor work can consume the schema confidently.
- **Dependencies:** Builds on CL-M-04.01 class-aware template model and validation.

---

## Scope

### In Scope ✅
1. Authoritative template schema (JSON + markdown) including classes/traffic.
2. Schema loader/validator in Sim (TemplateService/TemplateValidator path).
3. CLI validation/tests that exercise schema + semantic checks together.
4. Documentation updates that point to the schema as source of truth.

### Out of Scope ❌
- ❌ Engine-side template ingestion (Sim-only authoring surface).
- ❌ UI/editor consumption (covered in later UI milestones).
- ❌ Performance tuning beyond keeping validation deterministic.

---

## Requirements

### Functional Requirements

#### FR1: Schema Definition
- [ ] `docs/schemas/template.schema.json` (and `template-schema.md`) describe the class-aware template shape: `classes[]`, `traffic.arrivals[].classId`, topology, nodes, outputs, rng, provenance.
- [ ] Examples in docs reflect single-class (implicit `*`) and multi-class arrivals.

#### FR2: Schema-Backed Validation
- [ ] Sim template parsing applies JSON schema validation before semantic checks; clear errors when the schema is violated.
- [ ] Validation remains deterministic and side-effect free (no file/network access).
- [ ] Optional schema validation can be toggled for perf-sensitive workflows, but enabled by default in CLI validate/generate.

#### FR3: CLI & Tests
- [ ] `flow-sim validate` fails on schema violations (e.g., undeclared classId, missing required fields) and passes valid multi-class templates.
- [ ] Tests cover positive/negative schema validation and combined semantic validation.
- [ ] Tracking docs and milestone references updated to point to the new schema.

### Non-Functional Requirements
- **Backward Compatibility:** Templates without `classes` remain valid (implicit `*`), and existing fields keep current semantics.
- **Determinism:** Validation produces stable, order-independent errors; no network calls to fetch meta-schemas.

---

## Implementation Plan

### Phase 1: Schema Authoring
**Goal:** Land authoritative template schema and docs.
- [ ] Update `docs/schemas/template.schema.json` to include classes/traffic and current template fields.
- [ ] Refresh `docs/schemas/template-schema.md` with examples (single-class default and multi-class).
- [ ] Add schema examples matching `examples/class-enabled.yaml`.

### Phase 2: Runtime Validation
**Goal:** Wire schema validation into Sim pipeline.
- [ ] Add schema loader (local only, no meta fetch) and validate templates before semantic checks.
- [ ] Ensure failures surface actionable messages (field path + reason).
- [ ] Guardrail: fallback to semantic validation if schema cannot load (warn).

### Phase 3: CLI & Tests
**Goal:** Enforce schema in CLI and lock with tests.
- [ ] Add Sim tests for schema validation pass/fail cases (class-aware arrivals, missing classId, unknown classId).
- [ ] Add CLI tests for `flow-sim validate` reflecting the schema errors.
- [ ] Verify `flow-sim generate` still succeeds on valid class templates.

### Phase 4: Docs & Handoff
**Goal:** Publish references and close loop.
- [ ] Point template authoring docs to the new schema.
- [ ] Note schema availability in milestone/tracking follow-ups.
- [ ] Confirm examples validate against schema.

---

## Test Plan

- **Schema Validation Tests:** Cover allowed classes, missing/unknown classId, required fields, traffic patterns.
- **CLI Tests:** `flow-sim validate` fails with clear messages on schema errors; passes with multi-class templates.
- **Regression:** Ensure existing single-class templates still validate.
- **Determinism:** Re-run schema validation to confirm stable error text/order.

---

## Success Criteria
- [ ] Template schema JSON + markdown reflect class-aware shape.
- [ ] Sim pipeline applies schema validation by default; clear error messages.
- [ ] CLI validation/generate workflows pass with valid templates and fail on schema errors.
- [ ] Examples validate against the schema; docs point to it as source of truth.

---

## File Impact Summary
- `docs/schemas/template.schema.json` — authoritative schema update.
- `docs/schemas/template-schema.md` — narrative update + examples.
- `src/FlowTime.Sim.Core/Templates/*` — schema loader/integration.
- `src/FlowTime.Sim.Cli/*` — CLI validation wiring.
- `tests/FlowTime.Sim.Tests/*` — schema + CLI validation tests.

---

## Notes
- Keep validation local (no meta-schema fetch). Strip `$schema` if needed to avoid network calls.
- Preserve backward compatibility: implicit wildcard class when `classes` absent.
