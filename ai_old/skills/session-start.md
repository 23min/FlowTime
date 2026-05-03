# Skill: session-start

**Trigger phrases:** "start session", "begin work", "let's start", "initialize", "new session"

## Purpose

Initialize a working session by establishing context, choosing the appropriate agent role, and defining the task scope. This is the entry point for all AI-assisted work.

## Why Start Here

Starting with session-start ensures:
- Clear understanding of goals and context
- Appropriate agent persona is selected
- Correct skills are activated
- Guardrails are established
- Progress can be tracked effectively

## When to Use

**Always use this at the beginning of:**
- A new work session
- Switching between different types of work
- Returning to work after time away
- Collaborating with AI on an unfamiliar task

**Skip this only if:**
- Continuing active work in the same context
- Making trivial edits or quick fixes
- Already in an established session

## Process

### Step 1: Understand the Goal

Ask the user to describe their goal in one of these categories:

**Epic Work:**
- "I want to plan a new feature/capability" → leads to epic-refine
- "I need to start work on an epic" → leads to epic-start
- "An epic is complete and needs closure" → leads to epic-wrap

**Milestone Work:**
- "I want to create a milestone specification" → leads to milestone-draft
- "I want to implement milestone X" → leads to milestone-start
- "Milestone is done, need to wrap it up" → leads to milestone-wrap

**Development Tasks:**
- "I want to implement a feature" → leads to red-green-refactor
- "I need to write tests" → leads to red-green-refactor
- "I want to fix a bug" → leads to red-green-refactor
- "I need to review code" → leads to code-review
- "UI is not working correctly" → leads to ui-debug

**Documentation & Planning:**
- "I need to update documentation" → documenter agent
- "I want to plan the roadmap" → leads to roadmap skill
- "Found a gap or missing requirement" → leads to gap-triage

**Release & Deployment:**
- "Ready to release" → leads to release
- "Need to deploy" → leads to deployment

**Other:**
- "I'm not sure what to do" → continue with guided questions
- "Help me understand the codebase" → exploration mode

### Step 2: Select Agent Persona

Based on the goal, recommend the appropriate agent:

| Goal Type | Primary Agent | Why |
|-----------|---------------|-----|
| Epic planning, design decisions | **architect** | Focuses on scope, boundaries, and tradeoffs |
| Coding, bug fixes, features | **implementer** | Focuses on precise, safe code changes |
| Writing tests, TDD | **tester** | Focuses on test planning and coverage |
| Documentation, release notes | **documenter** | Focuses on clarity and consistency |
| Deployment, infrastructure | **deployer** | Focuses on release mechanics |

**Explain the choice:** Tell the user why this agent is appropriate and what to expect.

### Step 3: Gather Context

Collect the minimum necessary context:

**For epic work:**
- Epic name/slug
- High-level goal or problem
- Known constraints
- Related documentation (if any)

**For milestone work:**
- Milestone ID (e.g., M-02.10, SIM-M-03.00)
- Epic it belongs to (if known)
- Current status (planned, in-progress, complete)
- Location of spec and tracking docs

**For development tasks:**
- What needs to change?
- Which files or components?
- Are there existing tests?
- What's the expected behavior?

**For bugs:**
- Steps to reproduce
- Expected vs actual behavior
- Error messages or logs
- Which components are affected?

### Step 4: Confirm Guardrails

Remind the user (and yourself) of core principles:

**Always:**
- Follow TDD: write tests first (RED → GREEN → REFACTOR)
- Build and test before considering work complete
- Keep changes small and focused
- Document decisions inline
- Update relevant docs when behavior changes

**Never:**
- Include time or effort estimates
- Skip tests
- Make broad refactors without context
- Commit without running tests
- Merge to main without review

### Step 5: Establish Branch Context

If this is implementation work, confirm the branch strategy:

**Ask:**
- Are you on the correct branch for this work?
- Is this a new feature, milestone, or bug fix?
- Should we create a new branch?

**Branch naming conventions:**
- `epic/<epic-slug>` - epic integration branch
- `milestone/mX` - milestone integration branch
- `feature/<surface>-mX/<desc>` - feature work branch
- `fix/<desc>` - bug fix branch

### Step 6: Create or Resume Tracking

For milestone work:
- Check if tracking doc exists
- If starting fresh, create from tracking template
- If resuming, read current status and last completed phase
- Update tracking doc with session start time and goals

### Step 7: Hand Off to Skill

Based on the gathered context, explicitly hand off to the appropriate skill:

**Example handoffs:**
- "Starting epic-refine for [epic name]..."
- "Beginning milestone-start for M-02.10..."
- "Entering red-green-refactor cycle for [feature]..."
- "Running code-review on recent changes..."

## Outputs

Session-start produces:
1. **Chosen agent persona** with rationale
2. **Identified skill** to execute next
3. **Gathered context** sufficient to begin work
4. **Confirmed guardrails** for the session
5. **Active tracking** (for milestone work)
6. **Branch confirmation** (for implementation work)

## Template Prompt

When a user requests to start a session, use this template:

```
Welcome! Let's start your session.

**What would you like to accomplish?**
(Examples: plan an epic, implement a milestone, fix a bug, write tests, update docs, prepare release)

[Wait for response]

Based on that goal, I recommend the **[agent name]** persona.
This agent focuses on [agent's focus area] and typically uses these skills:
- [skill 1]
- [skill 2]

**Context check:**
- [Ask relevant context questions based on goal type]

**Guardrails reminder:**
- ✅ Tests first (RED → GREEN → REFACTOR)
- ✅ Build and test before handoff
- ✅ Small, focused changes
- ❌ No time estimates
- ❌ No untested code

**Ready to begin?** I'll now [describe which skill will run next].
```

## Notes

- Session-start is **not optional** for complex work - it prevents costly context-switching
- For quick edits or clarifications, you can skip session-start
- The chosen agent is a **guide**, not a constraint - switch agents if the work changes
- Update tracking docs frequently during implementation
- End sessions cleanly: summarize what was done, what's next, and any open questions

## Common Session Paths

**Path 1: New Epic**
```
session-start → architect + epic-refine → [gap clarification] → documenter + milestone-draft
```

**Path 2: Implement Milestone**
```
session-start → implementer + milestone-start → [create tracking] → tester + red-green-refactor → [tests pass] → documenter + milestone-wrap
```

**Path 3: Bug Fix**
```
session-start → implementer → [identify root cause] → tester + red-green-refactor → code-review
```

**Path 4: Documentation Update**
```
session-start → documenter → [identify affected docs] → [update docs] → code-review
```

**Path 5: Release**
```
session-start → documenter + milestone-wrap → [verify completeness] → release
```
