# UI-M-03.11 Implementation Tracking

**Milestone:** UI-M-03.11 — Artifacts Page Restoration  
**Started:** 2025-10-17  
**Status:** 📋 Not Started  
**Branch:** `feature/time-travel-ui-m3`  
**Assignee:** [TBD]

---

## Quick Links

- Milestone Document: docs/milestones/UI-M-03.11.md
- Roadmap: docs/architecture/time-travel/ui-m3-roadmap.md
- Contracts Reference: docs/reference/contracts.md
- Milestone Guide: docs/development/milestone-documentation-guide.md

---

## Current Status

### Overall Progress
- [ ] Phase 1: Discovery Adapter (0/3 tasks)
- [ ] Phase 2: UI Rendering (0/2 tasks)
- [ ] Phase 3: Navigation Integration (0/1 tasks)

### Test Status
- Unit Tests: 0 passing / 0 total
- Integration Tests: 0 passing / 0 total
- E2E Tests: 0 passing / 1 planned

---

## Progress Log

### 2025-10-17 - Session: Tracking Setup

Preparation:
- [x] Read milestone document
- [x] Review contracts for run.json, manifest.json, series/index.json

Next Steps:
- [ ] Begin Phase 1 Task 1.1 (run.json discovery + unit tests)

---

## Phase 1: Discovery Adapter

Goal: File-backed discovery of runs with robust parsing and diagnostics.

### Task 1.1: Scan Roots and Validate Candidates
Files:
- `ui/FlowTime.UI/Services/ArtifactsDiscovery.cs` (new)
- `ui/FlowTime.UI/Models/ArtifactRun.cs` (new)

Checklist (TDD order):
- [ ] Unit test: `Discovers_ValidRun_WithRunJsonPresent` (RED)
- [ ] Implement root scanning for `runs/<runId>/`
- [ ] Exclude folders missing `run.json`; record diagnostics

Status: ⏳ Not Started

### Task 1.2: Parse run.json + Warnings
Checklist:
- [ ] Unit test: `Parses_RunJson_GridAndWarnings` (RED)
- [ ] Parse `runId`, `source`, `createdUtc`, `grid`, `warnings`
- [ ] Normalize grid summary for display

Status: ⏳ Not Started

### Task 1.3: Probe Optional Files (manifest.json, series/index.json)
Checklist:
- [ ] Unit test: `Probes_OptionalFiles_SetsPresenceFlags` (RED)
- [ ] If present, parse minimal info (hash count, points) for badges
- [ ] Presence flags reflect availability

Status: ⏳ Not Started

---

## Phase 2: UI Rendering

Goal: Render runs in a table/grid with presence and warning badges.

### Task 2.1: Artifacts Page Rendering
Files:
- `ui/FlowTime.UI/Pages/Artifacts/ArtifactsPage.razor` (new)

Checklist:
- [ ] Integration test (if infra): renders rows for sample fixtures
- [ ] Columns: runId, source, createdUtc, grid, warnings, presence
- [ ] Warning badge with hover text

Status: ⏳ Not Started

### Task 2.2: Diagnostics Panel
Checklist:
- [ ] Integration test: excluded/invalid runs listed with reasons
- [ ] Empty state guidance when no runs found

Status: ⏳ Not Started

---

## Phase 3: Navigation Integration

Goal: Open action routes into Time‑Travel views with run context.

### Task 3.1: "Open in Time‑Travel" Action
Files:
- `ui/FlowTime.UI/Pages/Artifacts/ArtifactsPage.razor`

Checklist:
- [ ] Integration test: clicking Open routes to `/time-travel/dashboard?runId=...`
- [ ] Error path: if target view cannot bind run, show inline error + back link

Status: ⏳ Not Started

---

## Test Plan

TDD Approach:
- Start with unit tests for discovery adapter; then integrate fixtures for UI rendering.

Test Cases:
- Valid run listed with expected metadata
- Partial run listed with warning and presence flags
- Invalid run excluded with diagnostic entry
- Open action routes with runId and target shows run context

E2E (manual):
- Populate `data/runs/` with 2 valid + 1 partial + 1 invalid; verify UI behaviors

---

## Final Checklist

- [ ] Artifacts page lists runs from disk
- [ ] Open action bridges to Time‑Travel with runId
- [ ] Diagnostics/empty states implemented
