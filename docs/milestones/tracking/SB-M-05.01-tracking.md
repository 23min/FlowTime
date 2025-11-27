# SB-M-05.01 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](../../development/milestone-rules-quick-ref.md) for workflow.

**Milestone:** SB-M-05.01 — ServiceWithBuffer Node Type (Breaking Introduction)  
**Started:** 2025-11-27  
**Status:** 🔄 In Progress  
**Branch:** `milestone/sb-m-05.01`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** [`docs/milestones/SB-M-05.01.md`](../SB-M-05.01.md)
- **Service-With-Buffer Architecture:** [`docs/architecture/service-with-buffer/service-with-buffer-architecture.md`](../../architecture/service-with-buffer/service-with-buffer-architecture.md)
- **Milestone Guide:** [`docs/development/milestone-documentation-guide.md`](../../development/milestone-documentation-guide.md)

---

## Current Status

### Overall Progress
- [ ] Phase 1: Schema & Engine (0/3 tasks)
- [ ] Phase 2: Docs & Authoring Alignment (0/3 tasks)
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
- [ ] Flesh out phase/task breakdown below.
- [ ] Begin Phase 1 with schema RED tests for `serviceWithBuffer`.

**Blockers:**
- None

---

## Phase 1: Schema & Engine

**Goal:** Introduce `kind: serviceWithBuffer`, retire `kind: backlog`, and keep numerical behavior identical via migration tests.

### Task 1.1: Schema + Validator RED→GREEN
**Files:** `docs/schemas/model.schema.yaml`, schema/validator tests

**Checklist (Tests first):**
- [ ] RED: Add schema tests rejecting `backlog` and accepting `serviceWithBuffer`.
- [ ] GREEN: Update schema + validators to new kind.
- [ ] REFACTOR: Clean up template loader warnings/compat shims.

**Tests:**
- [ ] Schema test suite (`FlowTime.Tests`)

**Status:** ⏳ Not Started

### Task 1.2: Engine Wiring & Back-Compat Harness
**Files:** `src/FlowTime.Sim.Core`, `src/FlowTime.Core`, engine tests

**Checklist:**
- [ ] RED: Port existing backlog tests to serviceWithBuffer form (mechanical conversion).
- [ ] GREEN: Map new node kind through engine + contributions builder.
- [ ] REFACTOR: Remove public backlog references; keep internal shim only if needed.

**Tests:**
- [ ] `FlowTime.Core.Tests` backlog/serviceWithBuffer regression suite

**Status:** ⏳ Not Started

### Task 1.3: Template Migration Fixtures
**Files:** `templates/*.yaml`, `tests/FlowTime.Sim.Tests`

**Checklist:**
- [ ] Update canonical templates (transportation, warehouse) to use `serviceWithBuffer`.
- [ ] Regenerate/compare runs to ensure metrics stable.
- [ ] Document migration notes in milestone doc.

**Tests:**
- [ ] Golden run diff or analyzer validation for migrated templates.

**Status:** ⏳ Not Started

### Phase 1 Validation
- [ ] `dotnet build`
- [ ] Schema + engine unit tests green
- [ ] Golden template comparisons show no numerical regressions

---

## Phase 2: Docs & Authoring Alignment

**Goal:** Update architecture, authoring docs, and examples to highlight ServiceWithBuffer as the canonical pattern.

### Task 2.1: Architecture References
**Files:** `docs/architecture/whitepaper.md`, `docs/reference/engine-capabilities.md`

**Checklist:**
- [ ] Update node taxonomy to mention ServiceWithBuffer.
- [ ] Link to architecture epic/milestone.
- [ ] Remove backlog wording.

**Status:** ⏳ Not Started

### Task 2.2: Template Authoring Guide
**Files:** `docs/templates/template-authoring.md`

**Checklist:**
- [ ] Rename backlog section → ServiceWithBuffer.
- [ ] Document schedule semantics + queue badge expectation.
- [ ] Provide YAML snippet.

**Status:** ⏳ Not Started

### Task 2.3: Example Templates & Docs
**Files:** `templates/*.yaml`, `docs/examples/*.md`

**Checklist:**
- [ ] Update sample templates to serviceWithBuffer.
- [ ] Refresh README/overview text.
- [ ] Capture before/after notes in milestone doc.

**Status:** ⏳ Not Started

### Phase 2 Validation
- [ ] Docs lint (optional)
- [ ] Template examples build/run after migration

---

## Phase 3: UI + Analyzer/CLI Alignment

**Goal:** Treat ServiceWithBuffer as a first-class service node across API, UI, analyzers, and CLI messaging.

### Task 3.1: API Surface Tweaks
**Files:** `src/FlowTime.API/Services/GraphService.cs`, contracts

**Checklist:**
- [ ] Add `nodeLogicalType: serviceWithBuffer` (non-breaking addition).
- [ ] Ensure `/state_window` exposes schedule metadata for these nodes.

**Status:** ⏳ Not Started

### Task 3.2: UI Rendering & Chips
**Files:** `src/FlowTime.UI/*`

**Checklist:**
- [ ] Render serviceWithBuffer nodes as services with buffer badge.
- [ ] Show dispatch schedule chips/tooltips when available.
- [ ] Update topology inspector + node panels.

**Status:** ⏳ Not Started

### Task 3.3: Analyzer & CLI Messaging
**Files:** `src/FlowTime.Generator/*`, `src/FlowTime.Sim.Cli/*`

**Checklist:**
- [ ] Update analyzer warning strings to say “service with buffer”.
- [ ] Ensure CLI verbose output highlights buffer + schedule info.
- [ ] Add/adjust tests covering new wording.

**Status:** ⏳ Not Started

### Phase 3 Validation
- [ ] UI manual check (class-enabled template)
- [ ] Analyzer/CLI tests updated
- [ ] `dotnet build` + targeted UI/analyzer test suites

---

## Testing & Validation Checklist

- [ ] `dotnet build`
- [ ] `dotnet test --nologo`
- [ ] Targeted schema/engine/analyzer/UI tests (document in logs)
- [ ] Manual UI verification (serviceWithBuffer node badge + schedule chip)

---

## Issues Encountered

_None yet._

---

## Notes

- Keep schema/docs/CLI wording in sync when backlog → serviceWithBuffer references are removed.
- Any temporary backlog shims must be clearly documented and removed before closing the milestone.
