# Milestone Documentation Rules Summary

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

### 4. Separate Tracking During Implementation 🔄
- Create `docs/milestones/tracking/[MILESTONE-ID]-tracking.md` when work starts on work branch
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
2. Create tracking document (`docs/milestones/tracking/[MILESTONE-ID]-tracking.md`)
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

---

## Examples

**Good:** `docs/milestones/M2.9.md` (remove time estimate)
**Good:** `docs/milestones/M2.10.md` (remove "1-2 hours")
**Updated:** `docs/milestones/UI-M2.9.md` (now follows rules)

---

## Full Guide

See [`docs/development/milestone-documentation-guide.md`](../development/milestone-documentation-guide.md) for comprehensive documentation standards, templates, and examples.
