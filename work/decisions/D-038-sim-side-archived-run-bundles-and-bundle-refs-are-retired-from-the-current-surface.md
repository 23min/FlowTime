---
id: D-038
title: Sim-side archived run bundles and bundle refs are retired from the current surface
status: accepted
---

**Status:** active
**Context:** Sim still writes ZIP/archive bundle residue under `data/storage/runs/` and returns `bundleRef`, but there is no production caller using that archive layer as the real run contract.
**Decision:** Delete the Sim-side archived run-bundle layer in M-025. Remove ZIP writes to `data/storage/runs/`, remove `bundleRef`/`StorageRef` return values from orchestration responses, and keep the canonical run directory under `data/runs/<runId>/` as the runtime truth.
**Consequences:** Canonical runs remain the first-party runtime/query artifact. Portable bundle writing survives only as the deliberate E-18 Time Machine capability, not as a current Sim-side archive product surface.
