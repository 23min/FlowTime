# UI-M-03.10 Implementation Tracking

**Milestone:** UI-M-03.10 — UI Baseline & Build Health  
**Started:** 2025-10-17  
**Status:** 🚧 In Progress  
**Branch:** `feature/time-travel-ui-m3`  
**Assignee:** [TBD]

---

## Quick Links

- Milestone Document: docs/milestones/UI-M-03.10.md
- Roadmap: docs/architecture/time-travel/ui-m3-roadmap.md
- Milestone Guide: docs/development/milestone-documentation-guide.md

---

## Current Status

### Overall Progress
- [x] Phase 1: Navigation Skeleton (2/2 tasks)
- [x] Phase 2: Legacy Guarding & Build Health (3/3 tasks)

### Test Status
- Unit Tests: not run (nav scaffolding only)
- Integration Tests: not run
- E2E Tests: manual nav sweep completed in browser

---

## Progress Log

### 2025-10-17 - Session: Tracking Setup

Preparation:
- [x] Read milestone document
- [x] Read roadmap
- [ ] Create feature sub-branch (optional per task)
- [ ] Verify local build baseline

Next Steps:
- [ ] Begin Phase 1 Task 1.1 (nav group + placeholders)

---

### 2025-10-17 - Session: UI navigation scaffolding

Work:
- [x] Added Time-Travel nav group with dashboard/topology/run/artifact placeholders.
- [x] Hid Analyze route and swapped Simulate for milestone placeholder copy.
- [x] Added startup version log and verified solution build.

Validation:
- [x] `dotnet build FlowTime.sln` (warnings only; existing nullability warnings remain).
- [x] Manual nav sweep (browser: /time-travel dashboard/topology/run/artifacts plus /simulate placeholder).
- [x] Console log verified once (“FlowTime.UI started v{version}”).

Notes:
- Placeholder copy references upcoming UI-M3 milestones per charter.
- Startup log prints `FlowTime.UI started v{version}` once during boot.

---

## Phase 1: Navigation Skeleton

Goal: Add Time‑Travel menu with placeholder pages and working routes.

### Task 1.1: Add Time‑Travel Menu + Placeholder Pages
Files:
- `ui/FlowTime.UI/Layout/ExpertLayout.razor`
- `ui/FlowTime.UI/Pages/TimeTravel/Dashboard.razor`
- `ui/FlowTime.UI/Pages/TimeTravel/Topology.razor`
- `ui/FlowTime.UI/Pages/TimeTravel/RunOrchestration.razor`
- `ui/FlowTime.UI/Pages/TimeTravel/Artifacts.razor`

Checklist (TDD order):
- [ ] Component tests: nav renders expected menu items (if test infra available)
- [x] Add nav group and routes
- [x] Placeholder pages render titles (no data)

Status: ✅ Completed (tests deferred)

### Task 1.2: Wire Routes and Nav Highlighting
Files:
- `ui/FlowTime.UI/App.razor` or router host
- `ui/FlowTime.UI/Program.cs` (DI/services if needed)

Checklist:
- [x] Routes `/time-travel/dashboard`, `/time-travel/topology`, `/time-travel/run`, `/time-travel/artifacts`
- [x] Current item highlighting reflects route
- [x] Smoke test: navigate through all placeholders without errors

Status: ✅ Completed

---

## Phase 2: Legacy Guarding & Build Health

Goal: Disable or guard legacy pages; ensure clean build/boot and a minimal startup log.

### Task 2.1: Guard/Hide Analyze & Simulate Legacy Pages
Files:
- `ui/FlowTime.UI/Pages/Analyze/*.razor`
- `ui/FlowTime.UI/Pages/Simulate/*.razor`

Checklist:
- [x] Hide Analyze menu/routes for now
- [x] Simulate routes to a placeholder: "Temporarily unavailable under M3 (see UI‑M-03.12)"
- [x] Ensure navigation does not crash

Status: ✅ Completed

### Task 2.2: Startup Log Line
Files:
- `ui/FlowTime.UI/Program.cs`

Checklist:
- [x] Emit single startup line: app version only
- [x] Verify appears once in console

Status: ✅ Completed

### Task 2.3: Build & Boot Health
Checklist:
- [x] Full solution build passes
- [x] App boots with no uncaught exceptions and minimal warnings
- [x] Manual nav pass across main sections

Status: ✅ Completed

---

## Test Plan

TDD Approach:
- Prefer smoke/integration checks first (nav render, route navigation) then add component tests if infra exists.

Test Cases:
- Nav Routing: clicking each Time‑Travel item changes route and renders placeholder
- Legacy Guard: legacy menu items render placeholders; no console errors
- Build Health: full solution build passes; runtime exceptions absent

E2E (manual):
- Navigate: Dashboard → Topology → Run Orchestration → Artifacts; confirm route and titles

---

## Final Checklist

- [x] Navigation skeleton added and functional
- [x] App build + boot passes; no runtime exceptions on nav pass
- [x] Placeholders protect legacy areas
