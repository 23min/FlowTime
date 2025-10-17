# UI-M-03.12 Implementation Tracking

**Milestone:** UI-M-03.12 — Simulate → Gold Run Integration  
**Started:** 2025-10-17  
**Status:** 📋 Not Started  
**Branch:** `feature/time-travel-ui-m3`  
**Assignee:** [TBD]

---

## Quick Links

- Milestone Document: docs/milestones/UI-M-03.12.md
- Roadmap: docs/architecture/time-travel/ui-m3-roadmap.md
- Telemetry Capture Guide: docs/operations/telemetry-capture-guide.md
- Contracts Reference: docs/reference/contracts.md
- Milestone Guide: docs/development/milestone-documentation-guide.md

---

## Current Status

### Overall Progress
- [ ] Phase 1: Orchestrator Adapter (0/3 tasks)
- [ ] Phase 2: Simulate UI Integration (0/3 tasks)
- [ ] Phase 3: Diagnostics & Refresh (0/2 tasks)

### Test Status
- Unit Tests: 0 passing / 0 total
- Integration Tests: 0 passing / 0 total
- E2E Tests: 0 passing / 1 planned

---

## Progress Log

### 2025-10-17 - Session: Tracking Setup

Preparation:
- [x] Read milestone document
- [x] Review telemetry capture guide & contracts

Next Steps:
- [ ] Begin Phase 1 Task 1.1 (adapter interface + validation tests)

---

## Phase 1: Orchestrator Adapter

Goal: File-backed adapter to execute local pipeline and stream logs to UI.

### Task 1.1: Define Request/Result Models + Validation
Files:
- `ui/FlowTime.UI/Models/OrchestrationRequest.cs`
- `ui/FlowTime.UI/Models/OrchestrationResult.cs`

Checklist (TDD order):
- [ ] Unit test: `RequestValidation_TemplateIdRequired` (RED)
- [ ] Implement request defaults (outputRoot)
- [ ] Result model includes status, runId, summary

Status: ⏳ Not Started

### Task 1.2: Implement Adapter + Log Streaming
Files:
- `ui/FlowTime.UI/Services/Orchestration/SimOrchestrator.cs`

Checklist:
- [ ] Unit test: `Adapter_StreamsLogs_AndCompletes` (RED)
- [ ] Implement execution with incremental log callbacks
- [ ] Provide cancel hook (best effort)

Status: ⏳ Not Started

### Task 1.3: Output Validation Utility
Checklist:
- [ ] Unit test: `ValidateOutputs_FindsRunJsonAndRunId` (RED)
- [ ] Parse run.json; compute summary (grid, warnings)
- [ ] Optional presence flags for manifest and series/index

Status: ⏳ Not Started

---

## Phase 2: Simulate UI Integration

Goal: Start form → logs → success card with actions.

### Task 2.1: Start Form + Validation
Files:
- `ui/FlowTime.UI/Pages/Simulate/*.razor`

Checklist:
- [ ] Inputs: templateId, outputRoot (default), parameters (optional), label (optional)
- [ ] Start disabled until valid; Cancel available

Status: ⏳ Not Started

### Task 2.2: Log Viewer + Status
Checklist:
- [ ] Renders rolling logs with levels
- [ ] Shows status: queued/running/succeeded/failed/canceled

Status: ⏳ Not Started

### Task 2.3: Completion + Actions
Checklist:
- [ ] Validates outputs; shows summary (createdUtc, grid, warnings)
- [ ] Buttons: Open in Time‑Travel, View in Artifacts

Status: ⏳ Not Started

---

## Phase 3: Diagnostics & Refresh

Goal: Resilient error paths and post‑run discoverability.

### Task 3.1: Error/Partial Handling
Checklist:
- [ ] Friendly failure view with last 100 log lines
- [ ] Partial outputs produce diagnostics; Open disabled when `run.json` missing

Status: ⏳ Not Started

### Task 3.2: Artifacts Refresh Integration
Files:
- `ui/FlowTime.UI/Pages/Artifacts/ArtifactsPage.razor`

Checklist:
- [ ] Trigger refresh after success; highlight new run if feasible

Status: ⏳ Not Started

---

## Test Plan

TDD Approach:
- Unit tests for request validation, result parsing, and `run.json` summary; then adapter log streaming and a small integration smoke.

Test Cases:
- Request missing templateId → validation error
- Adapter streams logs and produces a `runId`
- Output validation finds `run.json` and computes summary
- Success card shows actions; Artifacts refresh shows new run

E2E (manual):
- Run a sample template; confirm logs, outputs, Artifacts listing, and Time‑Travel open

---

## Final Checklist

- [ ] Simulate can produce a gold run bundle
- [ ] Logs visible; cancel operable
- [ ] Outputs validated; run discoverable in Artifacts
- [ ] Time‑Travel open works with new runId
