---
id: G-012
title: Streaming Epic Not Formalized
status: open
---

### Why this is a gap

The streaming architecture working note (`work/epics/streaming/streaming-architecture-working-note.md`) is a draft with no epic number, no milestones, and no acceptance criteria. For the crystal ball, fresh arrivals data is a hard requirement — without it, predictions use stale data and the prediction horizon is degraded.

A workaround exists: rapid batch ingestion via E-15's batch pipeline (poll every 5 minutes). This gives a useful but degraded crystal ball. True real-time prediction needs the streaming epic to be real.

### When to revisit

After E-15's first dataset path proves batch ingestion works. At that point, the streaming note should be promoted to an epic with milestones.

### Reference

- `work/epics/streaming/streaming-architecture-working-note.md`
- `docs/notes/crystal-ball-predictive-projection.md` (requirement 2: fresh arrivals data)

---
