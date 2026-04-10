---
description: Scan for stale, orphaned, or drifted documentation and workspace artifacts. Produces a health report and optionally fixes issues.
name: doc-gardening
when_to_use: |
  - Periodic housekeeping between milestones
  - When you suspect docs have drifted from code
  - When work/gaps.md or work/decisions.md may be stale
  - When the user asks to clean up, audit, or tidy docs
responsibilities:
  - Detect status-surface drift (milestone spec vs tracking doc vs roadmap vs CLAUDE.md)
  - Find stale decisions (active decisions whose code has moved on)
  - Identify orphaned docs (files not referenced from any index or spec)
  - Check for TODO/FIXME comments without linked tracking
  - Flag gaps.md items that may already be resolved
output:
  - Health report (Markdown) listing findings by category
  - Optionally: direct fixes for simple drift (status updates, dead-link removal)
invoked_by:
  - reviewer agent (between milestones or during wrap)
  - any agent when the user requests a cleanup audit
---

# Skill: Doc Gardening

Scan the workspace for documentation health issues and produce an actionable report.

## Checklist

### 1. Status-Surface Drift

Check that milestone/epic status is consistent across all surfaces:
- [ ] Milestone spec status matches tracking doc status
- [ ] Epic milestone table matches individual milestone specs
- [ ] `ROADMAP.md` matches epic specs
- [ ] `CLAUDE.md` current work section is accurate
- [ ] `work/epics/epic-roadmap.md` (if present) is consistent

Report any mismatches with file:line references.

### 2. Stale Decisions

- [ ] Read `work/decisions.md`
- [ ] For each `active` decision, check whether the code has moved past it (decision is now just "how things are" and can be marked `implemented`)
- [ ] For each `active` decision, check whether a later decision supersedes it
- [ ] Flag decisions older than 60 days that are still `active`

### 3. Orphaned Files

- [ ] Scan `docs/` for files not referenced from any index, spec, or README
- [ ] Scan `work/epics/` for folders not listed in any epic spec or roadmap
- [ ] Scan `work/epics/unplanned/` for folders that have been promoted to a numbered epic (now duplicated)

### 4. Gaps Audit

- [ ] Read `work/gaps.md`
- [ ] For each gap, check whether it has been addressed by a completed milestone
- [ ] Flag resolved gaps for removal

### 5. Stale TODOs

- [ ] Search for `TODO`, `FIXME`, `HACK`, `XXX` in source code
- [ ] Check whether each has a linked tracking item (gap, milestone, or issue)
- [ ] Report untracked TODOs

### 6. Template Freshness

- [ ] Compare `.ai/templates/` against actual recently-created documents
- [ ] Flag templates that no recent doc follows (may be outdated)

## Output Format

```markdown
# Doc Health Report — {YYYY-MM-DD}

## Status Drift
- {finding with file references}

## Stale Decisions
- {decision ID}: {reason it may be stale}

## Orphaned Files
- {file path}: {why it appears orphaned}

## Resolved Gaps
- {gap description}: {evidence it's resolved}

## Untracked TODOs
- {file:line}: {TODO text}

## Template Drift
- {template}: {issue}

## Suggested Actions
1. {action}
2. {action}
```

## When to Fix vs Report

- **Fix directly:** Simple status updates, removing dead links, marking decisions as `implemented`
- **Report only:** Anything that changes semantics, deletes content, or requires a judgment call
