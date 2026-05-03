---
id: G-007
title: Client-Side Route Derivation for layoutFlow
status: open
---

FlowTime classes are metric dimensions, not graph routes. To use dag-map's `layoutFlow`, we need routes: `{ id, cls, nodes: [nodeIds] }`. The API provides `ByClass` on edges/nodes in state data, but no path-level query.

**Workaround:** Trace edges with non-zero `ByClass[classId].flowVolume` per class to derive approximate routes. Not authoritative — a proper Path Analysis API is needed for production.

**Status:** Not attempted yet. Needs experimentation.

---
