---
id: D-026
title: E-16-04 removes legacy analytical run paths forward-only
status: accepted
---

**Status:** active
**Context:** M-015 removes the duplicate analytical fallback in `MetricsService`. One open question was whether unsupported legacy runs should fail with an explicit regeneration message or be tolerated through an upgrade boundary.
**Decision:** Neither. E-16 remains strictly forward-only: legacy runs that depend on the old analytical/runtime boundary are deleted and replaced with regenerated runs. M-015 removes the fallback path without introducing an "unsupported, regenerate" runtime mode.
**Consequences:** `MetricsService.ResolveViaModelAsync()` and similar legacy analytical rescue paths are pure cleanup targets, not compatibility seams. Tests, fixtures, and local run directories that still depend on those paths must be regenerated or removed during the milestone.
