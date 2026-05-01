---
id: D-007
title: Fix P0 engine bugs before further Svelte UI work
status: accepted
---

**Status:** active
**Context:** Engine deep review found 3 P0 bugs (shared series mutation, missing capacity dependency, dispatch-unaware invariant). Svelte UI shows data from these APIs — incorrect engine data means incorrect visualization.
**Decision:** Prioritize Phase 0 bug fixes (BUG-1, BUG-2, BUG-3) before continuing Svelte UI M4 completion or M5/M6.
**Consequences:** Svelte UI work pauses briefly. Engine correctness gates all downstream work.
