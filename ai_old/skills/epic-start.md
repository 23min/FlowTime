# Skill: epic-start

**Trigger phrases:** "start epic", "begin epic work", "initialize epic", "resume epic", "epic context"

## Purpose

Initialize or confirm epic context before milestone work begins. This ensures you have the necessary background and setup to proceed with implementation.

## Use When
- Starting work on a new epic
- Returning to an epic after time away and context is unclear
- Switching between epics
- Team members joining ongoing epic work

## Inputs
- Epic name and slug
- Target branch strategy (epic integration or direct to mainline)

## Process

### 1. Locate Epic Documentation
- Find epic folder: `docs/architecture/<epic-slug>/`
- Read epic README for scope and goals
- Check roadmap for epic status and sequencing

### 2. Review Core Context
Gather understanding of:
- **Scope**: What's included and excluded
- **Goals**: What success looks like
- **Milestones**: Planned breakdown of work
- **Dependencies**: What must exist first
- **Status**: Where the epic stands now

### 3. Confirm or Create Epic Branch

**Branch naming:** `epic/<epic-slug>`

Example: `epic/ui-perf`, `epic/classes`, `epic/service-buffer`

**When to use epic branches:**
- Mainline (main) only advances when epics complete
- Multiple milestones in the epic need to integrate
- Epic spans significant time or complexity

**Commands:**
```bash
git checkout main
git pull
git checkout -b epic/<epic-slug>
git push -u origin epic/<epic-slug>
```

### 4. Summarize Epic Context
Produce a brief summary:
- Epic goal and value
- Planned milestones (in order)
- Current status (which milestones are done/in-progress/planned)
- Next milestone to work on

### 5. Hand Off to Milestone Work
Explicitly transition: "Ready to start milestone [ID]. Use milestone-start to begin."

## Outputs
- Epic context summary
- Confirmed branch plan
- Named milestone to start or continue
