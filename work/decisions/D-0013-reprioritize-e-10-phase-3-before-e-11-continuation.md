---
id: D-0013
title: Reprioritize E-0010 Phase 3 before E-0011 continuation
status: accepted
---

**Status:** active
**Context:** E-0010 Phase 3 (analytical primitives: cycle time, WIP limits, variability, constraint enforcement) was paused after Phases 0-2 to work on E-0011 Svelte UI. Phase 3 unlocks E-0012/E-0013/E-0014 downstream and the specs are all approved.
**Decision:** Resume E-0010 Phase 3 immediately (p3a → p3b → p3c → p3d). E-0011 Svelte UI paused after M6 until Phase 3 completes. Epics and milestones proceed in sequence from here.
**Consequences:** E-0011 M5/M7/M8 deferred. `milestone/m-svui-06` branch needs merge to main first. Next work: create `milestone/m-ec-p3a` from main.
