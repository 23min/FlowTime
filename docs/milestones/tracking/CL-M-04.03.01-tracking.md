# CL-M-04.03.01 Implementation Tracking

**Milestone:** CL-M-04.03.01 — Router Nodes & Class Routing Validation  
**Started:** 2025-11-26  
**Status:** 🚧 In Progress  
**Branch:** `feature/router-m4.3.1`

---

## Quick Links

- **Milestone Document:** `docs/milestones/CL-M-04.03.01.md`
- **Previous Release:** `docs/releases/CL-M-04.03.md`
- **Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Schema & Engine (Router Node)
- [ ] Phase 2: Template Updates & Docs
- [ ] Phase 3: Analyzer & API Integration

### Test Status
- ✅ `dotnet build` *(2025-11-26, router core pass)*
- ✅ `dotnet test --nologo`

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
- **RED:** Added `TemplateSchema_Allows_RouterDefinitions` / `TemplateSchema_Router_Requires_Target` plus `RouterClassContributionTests` covering router class routing + weights.
- **GREEN:** Extended `model.schema`, `ModelParser`, added `RouterNode`, and taught `ClassContributionBuilder` to compute router route contributions + overrides.

**Verification:**
- `dotnet build`
- `dotnet test --filter TemplateSchemaTests --nologo`
- `dotnet test --filter RouterClassContributionTests --nologo`

**Status:** ✅ Completed (commit `router-phase1` — 2025-11-26)

### 2025-11-27 - Phase 2 kickoff (templates/docs)

**Plan:**
- **RED:** Add FlowTime.Sim regression tests that assert router nodes exist in generated manifests for supply-chain + transportation templates (FlowTime.Api golden + CLI analyzers).  
- **GREEN:** Refactor `templates/transportation-basic-classes.yaml` and `templates/supply-chain-multi-tier-classes.yaml` to declare routers, regenerate runs via CLI harness, and refresh docs (`docs/templates/template-authoring.md`, `templates/README.md`).
- Capture analyzer + run IDs proving `classCoverage: full`.

**Next Commands (planned):**
- `dotnet test --filter "...RouterTemplate..." --nologo`
- `dotnet test --filter FlowTime.Api.Tests --nologo`
- `flowtime analyzers --target router-classes ...`

### 2025-11-27 - Phase 2 RED: router regression tests

**TDD Notes:**
- **RED:** Added `RouterTemplateRegressionTests` under `tests/FlowTime.Sim.Tests/Templates` to require router nodes inside canonical models generated from `transportation-basic-classes` and `supply-chain-multi-tier-classes`. Tests assert `HubDispatchRouter` (queue `hub_dispatch`) and `ReturnsRouter` (queue `returns_processed`) exist with expected targets.

**Status:** ✅ Tests added (see 2025-11-27 GREEN entry for implementation).

### 2025-11-27 - Phase 2 GREEN: template/router plumbing

**Implementation:**
- Extended template schema + validator + SimModelBuilder to support `kind: router` nodes end-to-end (TemplateNode, TemplateService, ModelService DTOs now copy `inputs` / `routes`).
- Refactored `transportation-basic-classes.yaml` (added `hub_dispatch_router`) and `supply-chain-multi-tier-classes.yaml` (`returns_router`) so canonical runs declare router nodes that map class flows to downstream arrivals.
- Updated `FlowTime.Contracts` pipeline so generated models keep router metadata for downstream analyzers/UI.

**Verification:**
- `dotnet test --filter RouterTemplateRegressionTests --nologo`

**Status:** 🟢 Router regression suite passing; need to regenerate runs/analyzers next.

### 2025-11-27 - Topology bin sampling regression fix

**Notes:**
- Observed `bin(t)` badge disappearing when scrubbing forward on the topology timeline; sampling used stale sparkline slices.
- Updated `BuildNodeSparklines` to accept an anchor bin so new selections rebuild slices before metrics sampling, and reordered `OnBinChanged` to rebuild first.

**Verification:**
- `dotnet build`
- `dotnet test --nologo` *(perf smoke skipped as expected)*

**Status:** 🟢 UI regression resolved; ready to continue with router templates/analyzers.

### 2025-11-27 - Topology bin(t) accuracy

**Notes:**
- After the sampling fix, `bin(t)` always displayed `1.0` because the sparkline primary series defaulted to success rate whenever it existed.
- Updated the sparkline builder to prefer the node’s canonical metric (arrivals/served/queue/etc.) and only fall back to success rate if nothing else is available so the badge reflects real values again.

**Verification:**
- `dotnet test --filter FlowTime.UI.Tests --nologo`

**Status:** 🟢 Badge shows actual per-node metric values.

### 2025-11-27 - Template analyzer sweep (transportation + supply chain)

**Notes:**
- Ran FlowTime.Sim CLI `generate` for `transportation-basic-classes` and `supply-chain-multi-tier-classes` to capture invariant analyzer output.
- Supply-chain template initially emitted `DistributionQueue` depth warnings because topology semantics pointed at `queue_inflow` while the backlog used `queue_demand`. Updated semantics to reference `queue_demand` (demand + backlog) so analyzer math and UI arrivals stay aligned.

**Verification:**
- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic-classes --templates-dir templates --mode simulation --out /tmp/transportation-basic-classes.yaml`
- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id supply-chain-multi-tier-classes --templates-dir templates --mode simulation --out /tmp/supply-chain-multi-tier-classes.yaml`

**Status:** 🟢 Template analyzer sweep clean; ready to regenerate reference runs.

### 2025-11-27 - Router documentation refresh

**Notes:**
- Expanded `docs/templates/template-authoring.md` with a dedicated “Router Nodes” section covering schema fields, analyzer expectations, and a sample snippet.
- Updated `docs/templates/template-testing.md` to include a router/class-coverage validation step and renumbered the workflow so authors record analyzer runs.

**Status:** 🟢 Docs reflect router authoring/testing guidance.

### 2025-11-27 - Transportation router targets fix

**Notes:**
- Observed that downstream dispatch queues still exposed all classes even after routing. Root cause: router routes targeted intermediate `*_inflow` nodes that are not tied to topology semantics, so class overrides never reached the queue metrics.
- Updated `templates/transportation-basic-classes.yaml` so router targets point directly at the queue demand nodes, removed the redundant `*_inflow` expressions, and refreshed regression tests/doc samples accordingly.
- Verified via deterministic engine run (`run_deterministic_1f945764`) that each dispatch queue now only reports its intended classes.

**Verification:**
- `dotnet test --filter RouterTemplateRegressionTests --nologo`
- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic-classes --templates-dir templates --mode simulation --out /tmp/transportation-basic-classes.yaml`
- `dotnet run --project src/FlowTime.Cli -- run /tmp/transportation-basic-classes.yaml --out data --deterministic-run-id --seed 4242`

**Status:** 🟢 Router outputs align with queue class filters.

### 2025-11-27 - Router propagation bug fix

**Notes:**
- After regenerating runs, leaf nodes still showed all classes because routers only rewrote their immediate targets; downstream expression/backlog nodes retained the pre-router class contributions.
- Updated `ClassContributionBuilder` so router overrides trigger a second pass that recomputes contributions for downstream nodes using the adjusted inputs, ensuring per-class CSVs reflect the routed traffic end-to-end.
- Confirmed via deterministic run `run_deterministic_40c00c5b` (seed 4242) that arrivals/leaf nodes now emit a single class.

**Verification:**
- `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter RouterTemplateRegressionTests --nologo`
- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic-classes --templates-dir templates --mode simulation --out /tmp/transportation-basic-classes.yaml`
- `dotnet run --project src/FlowTime.Cli -- run /tmp/transportation-basic-classes.yaml --out data --deterministic-run-id --seed 4242`

**Status:** 🟢 Router class splits propagate through downstream metrics.

### 2025-11-27 - Regenerated router reference runs

**Notes:**
- Generated deterministic simulation runs for both router-enabled templates using the Sim CLI + engine CLI workflow so RNG requirements are satisfied.
- Transportation run: `data/run_deterministic_1f945764` (seed 4242) — `classCoverage: "full"`, no warnings (replaced earlier run after router-class fix).
- Supply chain run: `data/run_deterministic_ff24907c` (seed 6789) — `classCoverage: "full"`, no warnings.

**Commands:**
- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic-classes --mode simulation --out /tmp/transportation-basic-classes.yaml`
- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id supply-chain-multi-tier-classes --mode simulation --out /tmp/supply-chain-multi-tier-classes.yaml`
- `dotnet run --project src/FlowTime.Cli -- run /tmp/transportation-basic-classes.yaml --out data --deterministic-run-id --seed 4242`
- `dotnet run --project src/FlowTime.Cli -- run /tmp/supply-chain-multi-tier-classes.yaml --out data --deterministic-run-id --seed 6789`

**Status:** 🟢 Canonical runs refreshed; ready to update golden fixtures/UI smoke tests.

---

## Phase 1: Schema & Engine (Router Node)

**Goal:** Define router nodes in the schema and core engine, guaranteeing per-class conservation.

### Task 1.1: Router Schema Definition (RED → GREEN)
**Checklist (Tests First):**
- [x] RED: Add failing schema tests ensuring `kind: router` plus `routes[].target/classes/weight` validation.
- [x] GREEN: Update `docs/schemas/model.schema.yaml` and schema docs to include router specification.

**Status:** ✅ Completed (2025-11-26)

### Task 1.2: Engine Support & Class Routing (RED → GREEN)
**Checklist (Tests First):**
- [x] RED: Add failing unit tests in `FlowTime.Core.Tests` verifying router splits classes correctly and conserves totals.
- [x] GREEN: Implement router evaluation (`ModelParser`, `ClassContributionBuilder`, execution pipeline) and ensure `byClass` metrics propagate.

**Status:** ✅ Completed (2025-11-26)

### Phase 1 Validation
- [x] Schema + engine tests green, router nodes available to templates.

---

## Phase 2: Template Updates & Docs

**Goal:** Refactor transportation and supply-chain templates to use routers; document usage.

### Task 2.1: Template Regression Tests (RED → GREEN)
**Checklist (Tests First):**
- [x] RED: Add FlowTime.Sim/template tests that expect router output for transport + supply-chain models.
- [ ] GREEN: Update templates (`transportation-basic-classes.yaml`, `supply-chain-multi-tier-classes.yaml`) with router nodes and regenerate sample runs/examples.

**Status:** 🚧 In Progress

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
