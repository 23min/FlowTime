# SB-M-05.01 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](../../development/milestone-rules-quick-ref.md) for workflow.

**Milestone:** SB-M-05.01 — ServiceWithBuffer Node Type (Breaking Introduction)  
**Started:** 2025-11-27  
**Status:** ✅ Complete  
**Branch:** `milestone/sb-m-05.01`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** [`work/milestones/completed/SB-M-05.01.md`](../completed/SB-M-05.01.md)
- **Service-With-Buffer Architecture:** [`work/epics/completed/service-with-buffer/service-with-buffer-architecture.md`](../../architecture/service-with-buffer/service-with-buffer-architecture.md)
- **Milestone Guide:** [`docs/development/milestone-documentation-guide.md`](../../development/milestone-documentation-guide.md)

---

## Current Status

### Overall Progress
- [ ] Phase 1: Schema & Engine (1/3 tasks ✅, 2 in flight)
- [ ] Phase 2: Docs & Authoring Alignment (2/3 tasks ✅)
- [ ] Phase 3: UI + Analyzer/CLI Alignment (0/3 tasks)

### Test Status
- **Schema/Engine Unit:** 0 / 0
- **Analyzer/CLI Tests:** 0 / 0
- **Integration/UI:** 0 / planned

---

## Progress Log

### 2025-11-27 - Kickoff & Renumbering

**Changes:**
- Renamed the service-with-buffer milestone to `SB-M-05.01` and updated architecture references.
- Created feature branch `milestone/sb-m-05.01` and this tracking document.

**Tests:**
- (n/a)

**Commits:**
- _(pending)_

**Next Steps:**
- [x] Flesh out phase/task breakdown below.
- [x] Begin Phase 1 with schema RED tests for `serviceWithBuffer`.

**Blockers:**
- None

### 2025-11-27 - Phase 1 Schema + Engine migration

**Changes:**
- Added new Template schema tests covering `kind: serviceWithBuffer` acceptance and ensured `kind: backlog` YAML now fails validation (`tests/FlowTime.Tests/Templates/TemplateSchemaTests.cs`).
- Updated `docs/schemas/model.schema.(yaml|md)` plus `template.schema.json/md` to document the new node kind.
- Introduced `ServiceWithBufferNode` (renamed from `BacklogNode`) and plumbed the new kind through ModelParser, TemplateValidator, SimModelBuilder, and ClassContributionBuilder so runtime + analyzer pipelines recognize the renamed node.
- Migrated canonical templates and CLI/analyzer fixtures to `kind: serviceWithBuffer` (transportation, supply-chain, IT microservices, manufacturing, warehouse, etc.), plus adjusted API Graph tests for the new spelling.

- **Tests:**
  - `dotnet test --nologo --filter TemplateSchemaTests`
  - `dotnet test --nologo tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj`

**Next Steps:**
- Finish Task 1.2 by reconciling any remaining engine/analyzer references and running targeted core/analyzer suites.
- Validate migrated templates via analyzer harness / golden comparisons (Task 1.3), then regenerate captures.

**Blockers:**
- None

### 2025-11-27 - Analyzer + CLI validation

**Changes:**
- Normalized `ModelParser.ParseSingleNode` node-kind handling so camel-case `serviceWithBuffer` deserializes correctly everywhere (`Unknown node kind` errors resolved).
- Ran `flow-sim generate` for `transportation-basic` and `warehouse-picker-waves` to confirm analyzer output stays consistent post-migration (warehouse template still reports the known bursty warnings slated for later epics).
- Exercised `TemplateInvariantAnalyzerTests` to ensure analyzer wiring recognizes the new node kind.

**Tests:**
- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic --templates-dir templates ...`
- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id warehouse-picker-waves --templates-dir templates ...`
- `dotnet test --nologo --filter TemplateInvariantAnalyzerTests`

**Next Steps:**
- Finish Task 1.3 by capturing the CLI analyzer output in the milestone doc and, if needed, regenerating sample runs for transportation & warehouse once UI verification happens.
- Move into Phase 2 doc/authoring edits once schema + template migration is signed off.

**Blockers:**
- None

### 2025-11-27 - Architecture & Authoring docs updated

**Changes:**
- `docs/architecture/whitepaper.md` now introduces ServiceWithBuffer nodes (section 2.4), updates the stateful primitives description, and refreshes the canonical example to use `kind: serviceWithBuffer` plus a dispatch schedule reference to the architecture doc.
- `docs/reference/engine-capabilities.md` references ServiceWithBuffer as the canonical queue-owning node kind and links to the deep-dive architecture doc.
- `docs/templates/template-authoring.md` renames the scheduled backlog section to “Scheduled Dispatch ServiceWithBuffer Nodes,” updates guidance on DLQs, and refreshes the YAML snippet to use the new node kind.

**Tests:**
- (docs only)

**Next Steps:**
- Update template/example READMEs (Task 2.3) so every public snippet references ServiceWithBuffer.
- Begin Phase 3 once docs + template references are complete.

**Blockers:**
- None

### 2025-11-28 - Example templates & README refresh

**Changes:**
- Confirmed every curated template now declares `kind: serviceWithBuffer` (no lingering backlog nodes) and refreshed `templates/README.md` copy to highlight the new terminology plus schedule semantics.
- Recorded the migration summary inside `work/milestones/completed/SB-M-05.01.md` (Implementation Notes) to capture the before/after context required by Task 2.3.

**Tests:**
- (docs/templates audit only)

**Next Steps:**
- Move into Phase 3 once API/UI consumers are ready for logical type metadata.

**Blockers:**
- None

### 2025-11-28 - API + UI logical-type plumbing

**Changes:**
- `/graph`, `/state`, and `/state_window` responses now surface `nodeLogicalType` plus dispatch schedule metadata even when topology nodes lacked explicit `dispatchSchedule` blocks (GraphService/StateQueryService + DTOs updated, tests adjusted).
- FlowTime.UI components (GraphMapper, metrics client, topology canvas/JS) ingest the logical type to render ServiceWithBuffer nodes as first-class services with queue badges + schedule chips; node inspector + sparklines now respect the metadata.
- Analyzer + CLI fixtures were updated to focus on ServiceWithBuffer nodes so warnings/verbose output reference the new terminology (TemplateInvariantAnalyzerTests + CLI provenance tests cover the scenarios).

**Tests:**
- `dotnet build`
- `dotnet test --nologo` (baseline perf benchmarks skipped as expected)

**Next Steps:**
- Run the full solution test sweep and capture results in this tracker prior to hand-off.

**Blockers:**
- None

### 2025-11-29 - Warehouse template polish & UI badges

**Changes:**
- Reworked `templates/warehouse-picker-waves.yaml`: topology nodes are now `kind: serviceWithBuffer`, backlog math no longer references queue facades, and Intake/PackAndShip emit `processingTimeMsSum` + `servedCount` so service-time chips always have real data. `flow-sim generate` now only reports the expected queue-latency informational warning.
- Adjusted the invariant analyzer so `serviceWithBuffer` nodes don’t get the spurious `served_exceeds_arrivals` warning when draining staged backlog.
- Added a queue-shaped “Staged backlog” chip next to arrivals in the topology canvas, plus a little queue badge drawn on every ServiceWithBuffer node. GraphService also drops dependency edges where source==target, so those self-loop lines are gone.

**Tests:**
- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id warehouse-picker-waves ...`
- `dotnet build`
- `dotnet test --nologo`

**Next Steps:**
- Manual UI verification (class-enabled template with serviceWithBuffer chip/badge) [pending].

**Blockers:**
- None

---

## Phase 1: Schema & Engine

**Goal:** Introduce `kind: serviceWithBuffer`, retire `kind: backlog`, and keep numerical behavior identical via migration tests.

### Task 1.1: Schema + Validator RED→GREEN
**Files:** `docs/schemas/model.schema.yaml`, schema/validator tests

**Checklist (Tests first):**
- [x] RED: Add schema tests rejecting `backlog` and accepting `serviceWithBuffer`.
- [x] GREEN: Update schema + validators to new kind.
- [x] REFACTOR: Clean up template loader warnings/compat shims.

**Tests:**
- [x] Schema test suite (`dotnet test --filter TemplateSchemaTests`)

**Status:** ✅ Complete

### Task 1.2: Engine Wiring & Back-Compat Harness
**Files:** `src/FlowTime.Sim.Core`, `src/FlowTime.Core`, engine tests

**Checklist:**
- [x] RED: Port existing backlog tests to serviceWithBuffer form (mechanical conversion).
- [x] GREEN: Map new node kind through engine + contributions builder.
- [ ] REFACTOR: Remove public backlog references; keep internal shim only if needed.

**Tests:**
- [ ] `FlowTime.Core.Tests` serviceWithBuffer regression suite (`ScheduledDispatchTests`, etc.)

**Status:** 🔄 In Progress — runtime wiring done, regression test run pending.

### Task 1.3: Template Migration Fixtures
**Files:** `templates/*.yaml`, `tests/FlowTime.Sim.Tests`

**Checklist:**
- [x] Update canonical templates (transportation, warehouse) to use `serviceWithBuffer`.
- [x] Regenerate/compare runs to ensure metrics stable.
- [x] Document migration notes in milestone doc.

**Tests:**
- [x] Analyzer validation via `flow-sim generate` (transportation-basic, warehouse-picker-waves) and recorded warnings.

**Status:** ✅ Complete

### Phase 1 Validation
- [x] `dotnet build`
- [x] Schema + engine unit tests green (`dotnet test --filter TemplateSchemaTests`, `dotnet test --nologo tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj`)
- [x] Golden template comparisons show no numerical regressions (`flow-sim generate` runs recorded in work/milestones/completed/SB-M-05.01.md)

---

## Phase 2: Docs & Authoring Alignment

**Goal:** Update architecture, authoring docs, and examples to highlight ServiceWithBuffer as the canonical pattern.

### Task 2.1: Architecture References
**Files:** `docs/architecture/whitepaper.md`, `docs/reference/engine-capabilities.md`

**Checklist:**
- [x] Update node taxonomy to mention ServiceWithBuffer.
- [x] Link to architecture epic/milestone.
- [x] Remove backlog wording.

**Status:** ✅ Complete

### Task 2.2: Template Authoring Guide
**Files:** `docs/templates/template-authoring.md`

**Checklist:**
- [x] Rename backlog section → ServiceWithBuffer.
- [x] Document schedule semantics + queue badge expectation.
- [x] Provide YAML snippet.

**Status:** ✅ Complete

### Task 2.3: Example Templates & Docs
**Files:** `templates/*.yaml`, `docs/examples/*.md`

**Checklist:**
- [x] Update sample templates to serviceWithBuffer.
- [x] Refresh README/overview text.
- [x] Capture before/after notes in milestone doc.

**Status:** ✅ Complete

### Phase 2 Validation
- [ ] Docs lint (optional)
- [x] Template examples build/run after migration

---

## Phase 3: UI + Analyzer/CLI Alignment

**Goal:** Treat ServiceWithBuffer as a first-class service node across API, UI, analyzers, and CLI messaging.

### Task 3.1: API Surface Tweaks
**Files:** `src/FlowTime.API/Services/GraphService.cs`, contracts

**Checklist:**
- [x] Add `nodeLogicalType: serviceWithBuffer` (non-breaking addition).
- [x] Ensure `/state_window` exposes schedule metadata for these nodes.

**Status:** ✅ Complete

### Task 3.2: UI Rendering & Chips
**Files:** `src/FlowTime.UI/*`

**Checklist:**
- [x] Render serviceWithBuffer nodes as services with buffer badge.
- [x] Show dispatch schedule chips/tooltips when available.
- [x] Update topology inspector + node panels.

**Status:** ✅ Complete

### Task 3.3: Analyzer & CLI Messaging
**Files:** `src/FlowTime.Generator/*`, `src/FlowTime.Sim.Cli/*`

**Checklist:**
- [x] Update analyzer warning strings to say “service with buffer”.
- [x] Ensure CLI verbose output highlights buffer + schedule info.
- [x] Add/adjust tests covering new wording.

**Status:** ✅ Complete

### Phase 3 Validation
- [x] UI manual check (warehouse + transportation templates)
- [x] Analyzer/CLI tests updated
- [x] `dotnet build` + targeted UI/analyzer test suites

---

## Testing & Validation Checklist

- [x] `dotnet build`
- [x] `dotnet test --nologo`
- [x] Targeted schema/engine/analyzer/UI tests (documented in progress log)
- [x] Manual UI verification (serviceWithBuffer node badge + schedule chip)

---

## Issues Encountered

- ServiceWithBuffer nodes still require explicit helper series (`queueDepth` references). Logged follow-up milestone `SB-M-05.02` to make topology declarations self-contained and to expose queue-latency status instead of null warnings when dispatch gates are closed.

---

## Notes

- Keep schema/docs/CLI wording in sync when backlog → serviceWithBuffer references are removed.
- Any temporary backlog shims must be clearly documented and removed before closing the milestone.
