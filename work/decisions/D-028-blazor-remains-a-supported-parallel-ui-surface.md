---
id: D-028
title: Blazor remains a supported parallel UI surface
status: accepted
---

**Status:** active
**Context:** Initial E-19 planning language assumed E-11 Svelte UI would replace and eventually retire the Blazor UI. The intended product direction is different: Svelte is a parallel UI track for demos and future evaluation, while Blazor remains useful for debugging, operational workflows, and as a plan-B surface if Svelte is not the long-term primary UI.
**Decision:** Keep `FlowTime.UI` as a supported first-party UI. E-11 builds Svelte in parallel rather than as a replacement. E-19 may remove stale compatibility wrappers and duplicate fallback logic from Blazor, but it does not retire Blazor or strip supported functionality; both UIs must stay aligned with evolving Engine/Sim contracts.
**Consequences:** Planning docs must not describe Svelte as a committed Blazor replacement. Cleanup milestones target stale compatibility seams, not supported Blazor capabilities. Reviews should treat Blazor functionality regressions caused by cleanup as bugs unless explicitly approved.
