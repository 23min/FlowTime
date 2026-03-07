# Skill: milestone-start

**Trigger phrases:** "start milestone", "begin milestone work", "implement milestone", "work on M-XX.XX"

## Purpose

Start or resume work on an existing milestone. This bridges planning and implementation.

## Use When
- Milestone spec exists and is marked 📋 Planned or 🔄 In Progress
- You're ready to begin coding
- Resuming milestone work after interruption

## Inputs
- Milestone ID (e.g., M-02.10, SIM-M-03.00, UI-M-02.09)
- Epic slug (if part of an epic)

## Preflight Check

**Before starting:**
- ✅ Milestone spec exists and is complete
- ✅ Dependencies are satisfied
- ✅ Epic context is clear (run epic-start if needed)
- ✅ Build and tests currently pass

## Process

### 1. Open Milestone Spec
- Location: `docs/milestones/<milestone-id>.md`
- Read: target, requirements, acceptance criteria
- Understand: phases, test plan, file impacts

### 2. Create Tracking Document

**First time starting milestone:**
- Create `docs/milestones/tracking/<milestone-id>-tracking.md`
- Copy from tracking template
- Populate: milestone ID, start date, TDD plan

**Tracking template structure:**
```markdown
# [Milestone ID] Tracking

**Status:** 🔄 In Progress
**Started:** [date]
**Branch:** [branch-name]

## Progress Summary
[Brief status update]

## Phases
### Phase 1: [Name]
- [ ] Task 1
- [ ] Task 2

### Phase 2: [Name]
- [ ] Task 1

## Test Results
[Record test outcomes]

## Decisions & Notes
[Document key choices]
```

### 3. Create or Confirm Branch

**Branch naming conventions:**
- Feature in milestone: `feature/<surface>-mX/<desc>`
- Milestone integration: `milestone/mX`
- Epic integration: `epic/<epic-slug>`

**Example commands:**
```bash
# Feature branch from milestone
git checkout milestone/m2
git checkout -b feature/api-m2/add-endpoint

# Or direct from main for simple changes
git checkout main
git pull
git checkout -b feature/api-m2/add-endpoint
```

### 4. Plan TDD Approach

List tests BEFORE writing implementation:
1. What behavior needs testing?
2. What are the edge cases?
3. What could break?
4. How will you know it works?

Update tracking doc with test plan.

### 5. Begin RED → GREEN → REFACTOR

Transition to red-green-refactor skill for implementation cycle.

Outputs:
- Active tracking doc.
- Branch ready for implementation.
