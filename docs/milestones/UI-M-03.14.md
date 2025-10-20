# UI-M-03.14 — Time-Travel Nav & Route Skeleton

**Status:** ✅ Complete  
**Dependencies:** ✅ UI-M-03.10 (UI Baseline & Build Health), ✅ UI-M-03.11 (Artifacts Page Restoration), ✅ UI-M-03.12 (Simulate → Gold Run Integration), ✅ UI-M-03.13 (Analyze Section Decision)  
**Target:** Establish the dedicated Time-Travel navigation group and placeholder routes (Dashboard, Topology, Run Orchestration, Artifacts) so downstream milestones can plug in visuals, orchestration, and adapters without churn.

---

## Overview

This milestone surfaces the Time-Travel workspace inside the Expert console by adding a persistent nav group and wiring skeleton pages for each upcoming view. It delivers the routing backbone that the Artifacts “Open” action and future gold-data experiences depend on, keeping UI work aligned with the roadmap sequence documented in `docs/architecture/time-travel/ui-m3-roadmap.md`.

Implementation already exists on `feature/time-travel-ui-m3`: the nav group lives in `src/FlowTime.UI/Layout/ExpertLayout.razor`, and the placeholder routes are implemented under `src/FlowTime.UI/Pages/TimeTravel/` (Dashboard, Topology, RunOrchestration, Artifacts).

### Strategic Context
- Motivation: Give operators a stable entry point into Time-Travel before dashboards, topology canvases, and orchestration experiences land.
- Impact: Artifacts and future “Open in Time-Travel” flows have deterministic destinations; downstream teams can iterate on page content without touching navigation.
- Dependencies: Baseline UI fixes, restored Artifacts workflow, Simulate → gold run integration, and the Analyze decision are complete so navigation changes won’t conflict with legacy surfaces.

---

## Scope

### In Scope ✅
1. Add a `Time-Travel` nav group under the Expert layout with links to Dashboard, Topology, Run Orchestration, and Artifacts (icons + labels per roadmap).
2. Register `/time-travel/dashboard`, `/time-travel/topology`, `/time-travel/run`, and `/time-travel/artifacts` routes with placeholder content that clearly states forthcoming functionality.
3. Ensure each page accepts the `runId` query parameter used by Artifacts deep links, and surface a friendly guidance message when no run context is supplied.

### Out of Scope ❌
- ❌ Rendering SLA tiles, topology canvases, or orchestration controls (handled by UI-M-03.20+ and UI-M-03.16).
- ❌ Time-Travel data service wiring (UI-M-03.15).
- ❌ Visual polish beyond minimal placeholders required to communicate upcoming work.

### Future Work
- UI-M-03.15 — Time-Travel data service (REST) populates the dashboard/topology pipeline.
- UI-M-03.16 — Run Orchestration page replaces the placeholder with real controls.
- Phase 2 milestones (UI-M-03.20+) attach visualizations and scrubber tooling.

---

## Requirements

### Functional Requirements

#### FR1: Expert Nav Group
- Description: Introduce a `Time-Travel` nav group in `ExpertLayout.razor` with four child links.
- Acceptance Criteria:
  - [x] Group label, icon, and ordering match the roadmap (Dashboard, Topology, Run Orchestration, Artifacts) as implemented in `src/FlowTime.UI/Layout/ExpertLayout.razor`.
  - [x] Selecting a link highlights the active route and loads the corresponding placeholder page.
  - [x] Navigation works in mini and expanded drawer modes.

#### FR2: Route Skeletons
- Description: Create placeholder Razor pages for the four `/time-travel/*` routes.
- Acceptance Criteria:
  - [x] Each route loads without runtime errors and sets an explicit `<PageTitle>` (`Pages/TimeTravel/*.razor`).
  - [x] Placeholder copy explains that visuals/controls will arrive in future milestones.
  - [x] Pages render safely with or without additional query parameters.

#### FR3: Run Context Handling
- Description: Align routes with Artifacts deep links that pass `runId`.
- Acceptance Criteria:
  - [x] Each placeholder page binds the optional `runId` query parameter.
  - [x] When `runId` is missing, the page surfaces guidance to pick a run from Artifacts (no unhandled errors).
  - [x] When `runId` is present, the placeholder acknowledges the run context in its messaging.

### Non-Functional Requirements
- **NFR1: Stable Entry Points** — Route and component names should remain stable through Phase 2; downstream work must not refactor navigation again.
- **NFR2: Truthful Messaging** — Placeholder content must accurately describe pending work and avoid implying completed functionality.

---

## Implementation Plan

### Phase 1: Navigation Structure
- Goal: Update `ExpertLayout.razor` with the Time-Travel nav group.
- Tasks:
  1. Add nav group with scoped icons and labels.
  2. Confirm drawer interactions (mini vs expanded) still operate.
- Success Criteria:
  - [x] Nav renders without layout regressions; lint/build succeed.

### Phase 2: Route Scaffolding
- Goal: Create placeholder pages for Dashboard, Topology, Run Orchestration, and Artifacts.
- Tasks:
  1. Scaffold Razor components under `Pages/TimeTravel/`.
  2. Add placeholder copy, runId binding, and basic back-navigation hints.
- Success Criteria:
  - [x] Visiting each route shows explanatory content and honors optional `runId`.

### Phase 3: Integration & Documentation
- Goal: Final verification and documentation alignment.
- Tasks:
  1. Smoke-test navigation paths manually.
  2. Update milestone tracking doc with status and findings.
- Success Criteria:
  - [x] Manual run-through recorded; docs reference the new entry points.

---

## Test Plan

- Manual:
  - Navigated via drawer to each `/time-travel/*` route in both mini and expanded modes (visual check on `feature/time-travel-ui-m3` branch).
  - Verified `/time-travel/dashboard?runId=<sample>` surfaces the run context note.
  - Verified `/time-travel/dashboard` without parameters shows the guidance message.
- Automated: No additional automated tests required for placeholder scaffolding.

---

## References

- `docs/architecture/time-travel/ui-m3-roadmap.md`
- `docs/milestones/UI-M-03.11.md`
- `docs/development/milestone-documentation-guide.md`
