# UI-M-03.10 Implementation Tracking

**Milestone:** UI-M-03.10 — UI Baseline & Build Health  
**Started:** 2025-10-17  
**Status:** 📋 Not Started  
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
- [ ] Phase 1: Navigation Skeleton (0/2 tasks)
- [ ] Phase 2: Legacy Guarding & Build Health (0/3 tasks)

### Test Status
- Unit Tests: 0 passing / 0 total
- Integration Tests: 0 passing / 0 total
- E2E Tests: 0 passing / 1 planned

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
- [ ] Add nav group and routes
- [ ] Placeholder pages render titles (no data)

Status: ⏳ Not Started

### Task 1.2: Wire Routes and Nav Highlighting
Files:
- `ui/FlowTime.UI/App.razor` or router host
- `ui/FlowTime.UI/Program.cs` (DI/services if needed)

Checklist:
- [ ] Routes `/time-travel/dashboard`, `/time-travel/topology`, `/time-travel/run`, `/time-travel/artifacts`
- [ ] Current item highlighting reflects route
- [ ] Smoke test: navigate through all placeholders without errors

Status: ⏳ Not Started

---

## Phase 2: Legacy Guarding & Build Health

Goal: Disable or guard legacy pages; ensure clean build/boot and a minimal startup log.

### Task 2.1: Guard/Hide Analyze & Simulate Legacy Pages
Files:
- `ui/FlowTime.UI/Pages/Analyze/*.razor`
- `ui/FlowTime.UI/Pages/Simulate/*.razor`

Checklist:
- [ ] Hide Analyze menu/routes for now
- [ ] Simulate routes to a placeholder: "Temporarily unavailable under M3 (see UI‑M‑03.12)"
- [ ] Ensure navigation does not crash

Status: ⏳ Not Started

### Task 2.2: Startup Log Line
Files:
- `ui/FlowTime.UI/Program.cs`

Checklist:
- [ ] Emit single structured startup line: app version, branch
- [ ] Verify appears once in console

Status: ⏳ Not Started

### Task 2.3: Build & Boot Health
Checklist:
- [ ] Full solution build passes
- [ ] App boots with no uncaught exceptions and minimal warnings
- [ ] Manual nav pass across main sections

Status: ⏳ Not Started

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

- [ ] Navigation skeleton added and functional
- [ ] App build + boot passes; no runtime exceptions on nav pass
- [ ] Placeholders protect legacy areas
