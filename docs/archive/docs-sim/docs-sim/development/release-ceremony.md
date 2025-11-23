# Release Ceremony

This document describes the step-by-step process for completing a release after merging to `main`.

## Purpose

Ensure consistency in versioning, tagging, documentation, and cleanup across all releases. This ceremony applies after any merge to `main` that represents a milestone completion or significant feature addition.

---

## Pre-Merge Checklist

Before merging to `main`, verify:

- [ ] All tests passing (automated test suite)
- [ ] Documentation updated (milestone docs, ROADMAP.md)
- [ ] Code reviewed (if team environment)
- [ ] No WIP or commented-out code
- [ ] Conventional commit messages used

---

## Post-Merge Ceremony

Execute these steps IN ORDER after merging to `main`.

### Step 1: Decide Version Number

Use the **Versioning Strategy** to determine the new version.

**Decision Tree:**

```
Is this a MAJOR milestone (M3.0, M4.0, M5.0, etc.)?
  → YES: Minor bump (0.6.0 → 0.7.0, 0.9.0 → 0.10.0, 0.11.0 → 0.12.0)
       Note: 0.x can go beyond 0.9! Use 0.10, 0.11, 0.50, etc.

Is this a SMALL milestone or feature addition (M2.10, M2.11, etc.)?
  → YES: Patch bump (0.6.0 → 0.6.1)

Is this only bug fixes within current milestone?
  → YES: Patch bump (0.6.0 → 0.6.1)

Is this production-ready with API stability commitment?
  → YES: Major bump to 1.0.0 (0.x.y → 1.0.0)

Is this work-in-progress toward next milestone?
  → YES: Add PreRelease suffix (0.7.0-preview)
```

**Examples:**
- M2.10 (small milestone, provenance queries) → `0.6.0` → `0.6.1` ✅
- M3.0 (major milestone, Graph API) → `0.6.1` → `0.7.0` ✅
- M4.0 (major milestone, Simulation) → `0.7.0` → `0.8.0` ✅
- M6.0 (major milestone) → `0.9.0` → `0.10.0` ✅
- Bug fix in registry → `0.6.0` → `0.6.1` ✅
- Production v1.0 launch → `0.x.y` → `1.0.0` ✅

**Rule:** Major milestones = Minor bump, Small milestones/features = Patch bump (in 0.x phase)

### Step 2: Update Version in All .csproj Files

**Files to update:**

```
src/FlowTime.Sim.Cli/FlowTime.Sim.Cli.csproj
src/FlowTime.Sim.Core/FlowTime.Sim.Core.csproj
src/FlowTime.Sim.Service/FlowTime.Sim.Service.csproj
```

**Edit each file:**

Change:
```xml
<VersionPrefix>0.6.0</VersionPrefix>
```

To:
```xml
<VersionPrefix>0.7.0</VersionPrefix>
```

**Command to find all version files:**
```bash
grep -r "VersionPrefix" src/**/*.csproj
```

### Step 3: Commit Version Bump

**Convention:** Use `chore(release):` prefix

```bash
git add src/**/*.csproj
git commit -m "chore(release): bump version to 0.7.0 for M2.10"
```

### Step 4: Create Release Document

**Location:** `docs/releases/M<X>.<Y>-v<version>.md`

**Naming Pattern:** `M2.10-v0.7.0.md` (milestone + semantic version)

**Template:**

```markdown
# M<X>.<Y>-v<major>.<minor>.<patch> Release Notes

**Release Date:** [Date]  
**Milestone:** M<X>.<Y> - [Milestone Name]  
**Version:** <major>.<minor>.<patch>  
**Status:** ✅ Complete

---

## 🎯 Milestone Goal

[High-level goal and what was achieved]

## ✅ What's New in v<version>

### [Feature Category]
- **Feature 1**: Description
- **Feature 2**: Description

### 🏗️ Architecture & Design
- [Architectural changes or patterns introduced]

### 🧪 Test Coverage
- [Test statistics and coverage highlights]

## 🔧 Technical Implementation

### Core Components:
[List files/modules added or significantly changed]

### API Changes:
[Document any API additions, changes, or deprecations]

### Breaking Changes:
[List any breaking changes - NONE for 0.x releases expected]

## 📊 Performance & Quality

- [Performance characteristics]
- [Test results summary]
- [Any notable metrics]

## 🚀 Development Impact

[How this affects developers using the system]

## 📝 Migration Notes

[If applicable, how to migrate from previous version]

---

**Previous Version:** <old-version>  
**Upgrade Path:** [Instructions if needed]
```

### Step 5: Commit Release Document

```bash
git add docs/releases/M2.10-v0.7.0.md
git commit -m "docs(release): add M2.10-v0.7.0 release notes"
```

### Step 6: Create and Push Git Tag

**Tag Format:** `v<major>.<minor>.<patch>`

**Create annotated tag:**
```bash
git tag -a v0.7.0 -m "Release v0.7.0 - M2.10 Provenance Query Support

- API query parameters for templateId and modelId
- CLI artifacts list command with provenance filters
- Offline CLI operation (no API required)
- Stable sorting for deterministic results
- 15/15 provenance tests passing"
```

**Push tag to origin:**
```bash
git push origin v0.7.0
```

### Step 7: Push Version Bump and Docs to Main

```bash
git push origin main
```

### Step 8: Clean Up Branches

**Delete local feature branch:**
```bash
git branch -d feature/api-m2.10/provenance-queries
```

**Delete remote feature branch (if it was pushed):**
```bash
git push origin --delete feature/api-m2.10/provenance-queries
```

**Delete milestone branch (if one was used):**
```bash
# Only if milestone/m2 or similar exists
git branch -d milestone/m2
git push origin --delete milestone/m2
```

### Step 9: Verification Checklist

Verify the release is complete:

- [ ] Version bumped in ALL .csproj files (check with grep)
- [ ] Release document created in `docs/releases/`
- [ ] Git tag exists: `git tag --list v0.7.0`
- [ ] Tag pushed to origin: `git ls-remote --tags origin | grep v0.7.0`
- [ ] Main branch pushed: `git log origin/main --oneline -3`
- [ ] Feature branches deleted locally and remotely
- [ ] ROADMAP.md shows milestone as complete
- [ ] Tests still passing: `dotnet test`

---

## Quick Reference Commands

**For M2.10 → v0.7.0 example:**

```bash
# Step 2-3: Version bump
# (Edit .csproj files manually or with sed)
git add src/**/*.csproj
git commit -m "chore(release): bump version to 0.7.0 for M2.10"

# Step 4-5: Release doc
# (Create docs/releases/M2.10-v0.7.0.md)
git add docs/releases/M2.10-v0.7.0.md
git commit -m "docs(release): add M2.10-v0.7.0 release notes"

# Step 6: Tag
git tag -a v0.7.0 -m "Release v0.7.0 - M2.10 Provenance Query Support"
git push origin v0.7.0

# Step 7: Push main
git push origin main

# Step 8: Cleanup
git branch -d feature/api-m2.10/provenance-queries
git push origin --delete feature/api-m2.10/provenance-queries  # if needed

# Step 9: Verify
git tag --list v0.7.0
git ls-remote --tags origin | grep v0.7.0
dotnet test
```

---

## Versioning Philosophy

**Milestone-Driven:**
- Major.Minor reflects capability level, not arbitrary breaking changes
- Patch for bug fixes and improvements within milestone
- Minor for milestone completions (0.x phase)
- Major for fundamental architecture changes or 1.0 stability

**Pre-1.0 (0.x.x):**
- API changes allowed between minor versions
- Focus on capability delivery over backward compatibility
- Breaking changes documented but expected

**Post-1.0 (1.x.x+):**
- Semantic versioning strictly enforced
- Breaking changes only in major versions
- Backward compatibility guaranteed for minor/patch

---

## Common Scenarios

### Scenario 1: Milestone Completion (Most Common)
- **Trigger:** Merged feature branch completing a milestone
- **Action:** Minor bump + release doc + tag
- **Example:** M2.10 complete → 0.6.0 → 0.7.0

### Scenario 2: Bug Fix
- **Trigger:** Hotfix merged to main
- **Action:** Patch bump + tag (release doc optional)
- **Example:** Registry fix → 0.7.0 → 0.7.1

### Scenario 3: Multiple Features in Same Milestone
- **Trigger:** Several PRs merged before milestone complete
- **Action:** Patch bumps during development, minor bump at completion
- **Example:** M3.0 feature 1 → 0.7.1, feature 2 → 0.7.2, completion → 0.8.0

### Scenario 4: Breaking Change (Rare in 0.x)
- **Trigger:** API contract change requiring consumer updates
- **Action:** Major bump + comprehensive migration guide
- **Example:** Schema v2 → 0.x.x → 1.0.0

---

## Troubleshooting

**Q: I forgot to bump the version before tagging**
```bash
# Delete local tag
git tag -d v0.7.0
# Delete remote tag (if pushed)
git push origin --delete v0.7.0
# Fix version, commit, re-tag
```

**Q: Version inconsistent across .csproj files**
```bash
# Find all occurrences
grep -r "VersionPrefix" src/**/*.csproj
# Update all files to match
# Amend commit if not yet pushed
git add src/**/*.csproj
git commit --amend --no-edit
```

**Q: Need to update release doc after tagging**
```bash
# Just edit and commit - tags point to commits, not files
git add docs/releases/M2.10-v0.7.0.md
git commit -m "docs(release): update M2.10 release notes"
git push origin main
```

---

## Integration with CI/CD

**Future:** When CI/CD is configured:
- Version bump triggers automated build
- Tag creation triggers release artifacts
- Release doc uploaded to GitHub Releases
- Changelog auto-generated from commits

**Current:** Manual ceremony ensures consistency until automation is ready.

---

**Last Updated:** October 7, 2025  
**Applies To:** FlowTime-Sim v0.3.0+
