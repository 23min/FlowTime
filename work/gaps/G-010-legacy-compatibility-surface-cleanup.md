---
id: G-010
title: Legacy / Compatibility Surface Cleanup
status: open
---

### Why this was a gap

E-16 owns analytical truth and consumer-fact purification, but broader non-analytical compatibility debt still remains across first-party UI, Sim, docs, schemas, and examples:

- first-party UI endpoint and metrics fallbacks
- legacy demo/template generation on active surfaces
- deprecated schema/example material living on current paths instead of archive/historical paths
- Blazor parallel-support and sync discipline still implicit rather than sequenced work

These do not belong inside E-16's analytical boundary, but they still need an owner.

### Status

Promoted to epic planning as `work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md`.

### Immediate implications

- Do not add new compatibility helpers to first-party UI/Sim/docs/example surfaces without explicit exit criteria.
- Prefer archive-or-delete over "keep both for now" once replacement surfaces are confirmed.
- Do not strip supported functionality from Blazor as part of this cleanup; keep it aligned with current Engine/Sim contracts.
- Treat E-19 as the post-E-16 cleanup lane; it should start after E-16 but does not automatically block E-10 Phase 3 resume.

---
