---
description: Assess and maintain per-surface quality scorecards. Tracks test coverage, tech debt, doc freshness, and convention compliance.
name: quality-score
when_to_use: |
  - After wrapping a milestone (as part of review)
  - When the user asks "how healthy is surface X?"
  - Periodically to track quality trends
responsibilities:
  - Compute or estimate test coverage per surface
  - Inventory known tech debt and unresolved TODOs
  - Check doc freshness (last meaningful update vs last code change)
  - Verify convention compliance (naming, structure, patterns)
  - Produce a scorecard and update the quality tracking file
output:
  - Quality scorecard (Markdown) per surface
  - Updated quality tracking file at the path defined by the repo
invoked_by:
  - reviewer agent (during wrap-milestone or on request)
  - any agent when the user asks about quality
---

# Skill: Quality Score

Assess and record quality metrics per project surface.

## Surfaces

A "surface" is a deployable or testable unit — e.g., `Core`, `API`, `Sim.Core`, `UI`.
The repo defines its surfaces; the framework defines how to score them.

## Checklist

### 1. Test Coverage

- [ ] Run tests with coverage enabled (or estimate from test file inventory)
- [ ] Record: total tests, passing, coverage % (line or branch)
- [ ] Note any untested public APIs or critical paths

### 2. Tech Debt Inventory

- [ ] Count `TODO`, `FIXME`, `HACK` comments in the surface
- [ ] Count items in `work/gaps.md` targeting this surface
- [ ] Note any known workarounds or compatibility shims

### 3. Doc Freshness

- [ ] Check `docs/` files related to this surface
- [ ] Compare last meaningful doc update date vs last code change date
- [ ] Flag docs that are > 30 days stale relative to code

### 4. Convention Compliance

- [ ] Spot-check naming (fields, methods, files) against project conventions
- [ ] Check for deprecated patterns the project has moved away from
- [ ] Verify JSON schema compliance (camelCase, no deprecated fields)

### 5. Build Health

- [ ] Build succeeds with no warnings (or note warning count)
- [ ] No analyzer suppressions without justification

## Scorecard Format

Use the `quality-scorecard` template. Store at `work/quality/{surface}.md` or wherever the repo configures.

## Trends

When updating a scorecard, preserve the previous snapshot in a `## History` section so trends are visible. Keep the last 5 snapshots; archive older ones.
