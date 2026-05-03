---
id: D-017
title: E-16 owns full purification and the migration is forward-only
status: accepted
---

**Status:** active
**Context:** The p3a1 pressure test showed that moving the current analytical capability/computation surface into Core was the right bridge, but full purification is larger than one E-10 follow-on milestone. Compiled semantic references, class-truth separation, runtime analytical descriptors, contract redesign, and consumer heuristic deletion all need one clear owner. We also do not want compatibility shims for old runs, fixtures, or hint-based contracts to dilute the cleanup.
**Decision:** Wrap `m-ec-p3a1` as the bridge milestone and assign the full formula-first purification to E-16. E-16 is forward-only: old run directories, generated fixtures, and approved golden snapshots can be deleted and regenerated; contract cleanup does not need additive compatibility phases once the named consumers for a milestone are migrated.
**Consequences:** E-10 Phase 3 pauses after `m-ec-p3a1` until E-16 completes. Milestone planning should prefer explicit deletion and regeneration over fallback layers. Reviews should treat new compatibility heuristics around the old analytical/runtime boundary as regressions.
