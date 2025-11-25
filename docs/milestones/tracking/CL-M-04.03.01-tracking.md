# CL-M-04.03.01 Implementation Tracking

**Milestone:** CL-M-04.03.01 — Router Nodes & Class Routing Validation  
**Started:** 2025-11-26  
**Status:** 📋 Planned  
**Branch:** `feature/router-m4.3.1`

---

## Quick Links

- **Milestone Document:** `docs/milestones/CL-M-04.03.01.md`
- **Previous Release:** `docs/releases/CL-M-04.03.md`
- **Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [ ] Phase 1: Schema & Engine (Router Node)
- [ ] Phase 2: Template Updates & Docs
- [ ] Phase 3: Analyzer & API Integration

### Test Status
- ⏳ `dotnet build`
- ⏳ `dotnet test --nologo`

---

## Progress Log

### 2025-11-26 - Kickoff

**Preparation:**
- [x] Reviewed CL-M-04.03.01 milestone scope (router nodes, template updates, analyzer work).
- [x] Created feature branch `feature/router-m4.3.1`.
- [x] Added tracking document for milestone.

**Next Steps:**
- Phase 1 RED: add failing schema + engine tests for router node definition/conservation.
- Document TDD steps in tracker as each phase proceeds.

### 2025-11-26 - Phase 1 Router schema & contributions

**TDD Notes:**
- RED: Added `TemplateSchema_Allows_RouterDefinitions` / `TemplateSchema_Router_Requires_Target` plus `RouterClassContributionTests` covering router class routing + weights.
- GREEN: Extended `model.schema`, `ModelParser`, added `RouterNode`, and taught `ClassContributionBuilder` to compute router route contributions + overrides.

**Commands:**
- `dotnet test --filter TemplateSchemaTests --nologo`
- `dotnet test --filter RouterClassContributionTests --nologo`

---

## Phase 1: Schema & Engine (Router Node)

**Goal:** Define router nodes in the schema and core engine, guaranteeing per-class conservation.

### Task 1.1: Router Schema Definition (RED → GREEN)
**Checklist (Tests First):**
- [ ] RED: Add failing schema tests ensuring `kind: router` plus `routes[].target/classes/weight` validation.
- [ ] GREEN: Update `docs/schemas/model.schema.yaml` and schema docs to include router specification.

**Status:** ⏳ Not Started

### Task 1.2: Engine Support & Class Routing (RED → GREEN)
**Checklist (Tests First):**
- [ ] RED: Add failing unit tests in `FlowTime.Core.Tests` verifying router splits classes correctly and conserves totals.
- [ ] GREEN: Implement router evaluation (`ModelParser`, `ClassContributionBuilder`, execution pipeline) and ensure `byClass` metrics propagate.

**Status:** ⏳ Not Started

### Phase 1 Validation
- [ ] Schema + engine tests green, router nodes available to templates.

---

## Phase 2: Template Updates & Docs

**Goal:** Refactor transportation and supply-chain templates to use routers; document usage.

### Task 2.1: Template Regression Tests (RED → GREEN)
**Checklist (Tests First):**
- [ ] RED: Add FlowTime.Sim/template tests that expect router output for transport + supply-chain models.
- [ ] GREEN: Update templates (`transportation-basic-classes.yaml`, `supply-chain-multi-tier-classes.yaml`) with router nodes and regenerate sample runs/examples.

**Status:** ⏳ Not Started

### Task 2.2: Documentation
**Checklist:**
- [ ] Update relevant docs (`docs/templates/template-authoring.md`, README, etc.) with router guidance and examples.

**Status:** ⏳ Not Started

### Phase 2 Validation
- [ ] Regenerated runs show `classCoverage: "full"` with routers handling splits; docs explain router semantics.

---

## Phase 3: Analyzer & API Integration

**Goal:** Extend analyzers, CLI, and API logging to enforce router conservation and surface metadata.

### Task 3.1: Analyzer Tests (RED → GREEN)
**Checklist (Tests First):**
- [ ] RED: Add failing analyzer tests for router leakage + missing class routes.
- [ ] GREEN: Implement analyzer logic + CLI command, log router diagnostics in `StateQueryService`.

**Status:** ⏳ Not Started

### Task 3.2: API/State Metadata
**Checklist:**
- [ ] Ensure `/state_window` serializer includes router metadata for UI consumers.
- [ ] Validate analyzer output logged in tracker (include run IDs).

**Status:** ⏳ Not Started

### Phase 3 Validation
- [ ] Analyzer harness PASS on regenerated runs, router metadata visible via API logs/state responses.

---

## Final Checklist
- [ ] `dotnet build`
- [ ] `dotnet test --nologo`
- [ ] Analyzer harness results documented with run IDs.
- [ ] Release notes updated once milestone completes.
