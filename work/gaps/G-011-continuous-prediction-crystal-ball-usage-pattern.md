---
id: G-011
title: Continuous Prediction / Crystal Ball Usage Pattern
status: open
---

### Why this is a gap

The crystal ball capability — feeding observed arrivals into a calibrated model to predict future system state faster than real time — emerges from the intersection of E-15 (topology inference + telemetry), E-18 (headless evaluation), and streaming (real-time ingestion). But no single epic owns this usage pattern, and design decisions in each epic could accidentally make continuous prediction harder.

The roadmap thesis (Pure → Interactive → Programmable) covers the building blocks but misses this third mode of operation:
- **E-17** (Interactive) assumes a human adjusting sliders.
- **E-18** (Programmable) assumes a pipeline running parameter sweeps.
- **Crystal ball** needs a continuously refreshed model without a human in the loop — a session without a user.

### Immediate implications

- E-18 spec work should consider "continuous evaluation with external data feed" as a first-class use case alongside batch parameter sweeps.
- Streaming epic promotion from working note to real epic should reference the crystal ball framing as a motivating use case.
- E-17's session management design should not assume sessions are always human-initiated.

### Reference

- `docs/notes/crystal-ball-predictive-projection.md`
- `docs/notes/predictive-systems-and-uncertainty.md`
- `work/epics/streaming/streaming-architecture-working-note.md`

---
