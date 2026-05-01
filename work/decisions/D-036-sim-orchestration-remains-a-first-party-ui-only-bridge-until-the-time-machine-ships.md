---
id: D-036
title: Sim orchestration remains a first-party UI-only bridge until the Time Machine ships
status: accepted
---

**Status:** active
**Context:** `POST /api/v1/orchestration/runs` is the live first-party UI path today, but letting it attract new callers would turn a transitional Sim host into a support obligation.
**Decision:** Keep Sim orchestration only as a first-party UI bridge. `/api/v1/orchestration/runs` remains supported for Blazor and Svelte, `/api/v1/drafts/run` remains only as the narrower inline-YAML path, no new non-UI callers land on either surface, and both are deleted by default when the Time Machine ships unless an E-18 milestone documents a concrete temporary facade requirement.
**Consequences:** M-025 narrows `drafts/run` to inline-only, but the final sunset belongs to E-18. New programmable or external integration work must not target Sim orchestration.
