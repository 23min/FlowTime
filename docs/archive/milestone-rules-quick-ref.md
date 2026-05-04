# Milestone Documentation Rules Summary

> **Archived 2026-05-03.** This document describes a pre-aiwf-v3 workflow (milestone rules quick reference). For current authority, see `CLAUDE.md` and the `aiwf-extensions` plugin skills (`aiwfx-plan-milestones`, `aiwfx-start-milestone`, `aiwfx-wrap-milestone`, `aiwfx-release`). Kept for historical reference only — do not follow as current guidance.

---

**Created:** 2025-10-06  
**Purpose:** Quick reference for milestone creation and maintenance

---

## Core Rules

### 1. NO Time Estimates ❌
- Never include hours, days, weeks, or effort estimates
- Never include target dates for future work
- Only completion dates for historical milestones (✅ Complete)

### 2. Graphics: Mermaid Only ✅
- ✅ Use Mermaid diagrams for architecture, sequences, flows
- ❌ Avoid ASCII art boxes and diagrams
- Mermaid is easier to read and maintain

### 3. Milestone = Foundation 📋
- Must be sufficient for implementation planning
- Must enable TDD plan creation before coding
- Self-contained and authoritative

### 4. Milestone ID Format 📛
- Engine milestones use `M-XX.XX` (e.g., `M-02.10`).
- FlowTime.Sim milestones use `SIM-M-XX.XX`.
- FlowTime.UI milestones use `UI-M-XX.XX`.
- Additional prefixes require architecture approval; CLI/tooling work typically falls under Engine or Sim milestones.

### 5. Separate Tracking During Implementation 🔄
- Create `work/milestones/tracking/[MILESTONE-ID]-tracking.md` when work starts on work branch
- Update tracking doc with each commit
- Keep milestone doc stable, tracking doc dynamic

---

## Required Sections

### Minimum Structure
```markdown
# [ID] — [Title]
**Status:** [📋 Planned | 🔄 In Progress | ✅ Complete]
**Dependencies:** [Prerequisites]
**Target:** [One-sentence goal]

## Overview
[What, Why, Impact]

## Scope
### In Scope ✅
### Out of Scope ❌

## Requirements
### Functional Requirements
### Non-Functional Requirements

## Implementation Plan
### Phase 1: [Name]
### Phase 2: [Name]

## Test Plan
### TDD Approach
### Test Cases

## Success Criteria
- [ ] Checklist items

## File Impact Summary
### Files to Modify
### Files to Create
```

---

## Anti-Patterns

### ❌ DON'T
- Include time/effort estimates
- Use ASCII art diagrams
- Write vague requirements ("make it faster")
- Skip test plan
- Omit scope boundaries
- Include target dates

### ✅ DO
- Use Mermaid diagrams
- Write testable acceptance criteria
- Define clear scope (in/out)
- Include comprehensive test plan
- Link related documents
- Update tracking doc during implementation

---

## Workflow

### Before Implementation
1. Write milestone document
2. Get review/approval
3. Mark milestone as 📋 Planned

### Starting Implementation
1. Create feature/work branch
2. Create tracking document (`work/milestones/tracking/[MILESTONE-ID]-tracking.md`)
3. Create TDD plan (as first step in tracking doc)
4. Write failing tests (TDD)

### During Implementation
1. Update tracking doc after each commit
2. Check off tasks in tracking doc
3. Keep milestone doc stable
4. Update milestone status to 🔄 In Progress
5. Update ROADMAP.md status

### After Completion
1. Mark milestone ✅ Complete
2. Update ROADMAP.md
3. Create release notes
4. Archive tracking document
5. **Await stakeholder approval before starting any follow-up milestone.** Do not branch or begin the next milestone until (a) the completed milestone’s acceptance criteria have been manually revalidated (e.g., rerun UI checks, regenerate runs) and (b) the milestone owner explicitly signs off on the hand-off.
6. **Run analyzers/invariant checks and document the outcome.** For any milestone that introduces new data planes (e.g., class-aware telemetry), execute the corresponding analyzers (class conservation, propagation, telemetry validation, etc.) and record the results in the tracking doc before marking the milestone complete. If an analyzer surfaces warnings, treat the milestone as ⚠️ until the warnings are resolved or explicitly waived by the owner.

---

## Quick Checklist

Before marking milestone as "📋 Planned":
- [ ] No time estimates anywhere
- [ ] Graphics use Mermaid (not ASCII)
- [ ] Requirements are testable
- [ ] Test plan enables TDD
- [ ] Success criteria measurable
- [ ] Scope clearly defined (in/out)
- [ ] File impact list included

### Naming Notes for Upcoming Epics

- Use architecture folders under `docs/architecture` for epic-level designs (for example, `docs/architecture/classes` and `docs/architecture/edge-time-bin`).
- When introducing new epics like **Classes as Flows** or **EdgeTimeBin Foundations**, align milestone IDs with existing prefixes (e.g., `M-03.xx` for engine, `SIM-M-03.xx` for Sim) and reference the corresponding architecture docs instead of duplicating design details.

---

## Examples

**Good:** `work/epics/completed/artifacts-schema-provenance/M-02.09.md` (remove time estimate)
**Good:** `work/epics/completed/artifacts-schema-provenance/M-02.10.md` (remove "1-2 hours")
**Updated:** `work/epics/completed/ui-schema-migration/UI-M-02.09.md` (now follows rules)

---

## Full Guide

See [`docs/development/milestone-documentation-guide.md`](../development/milestone-documentation-guide.md) for comprehensive documentation standards, templates, and examples.
