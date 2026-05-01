---
id: D-013
title: Reprioritize E-10 Phase 3 before E-11 continuation
status: accepted
---

**Status:** active
**Context:** E-10 Phase 3 (analytical primitives: cycle time, WIP limits, variability, constraint enforcement) was paused after Phases 0-2 to work on E-11 Svelte UI. Phase 3 unlocks E-12/E-13/E-14 downstream and the specs are all approved.
**Decision:** Resume E-10 Phase 3 immediately (p3a → p3b → p3c → p3d). E-11 Svelte UI paused after M6 until Phase 3 completes. Epics and milestones proceed in sequence from here.
**Consequences:** E-11 M5/M7/M8 deferred. `milestone/m-svui-06` branch needs merge to main first. Next work: create `milestone/m-ec-p3a` from main.
