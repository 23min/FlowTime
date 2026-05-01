---
id: D-035
title: E-19 shared framing locks the current component roles without renames
status: accepted
---

**Status:** active
**Context:** M-024 needed one authoritative framing for current cleanup so every later deletion or retention call used the same component boundaries.
**Decision:** No project renames happen in E-19. `FlowTime.Core` remains the pure evaluation library, `FlowTime.API` remains the query/operator surface over canonical runs, `FlowTime.Sim.Service` remains the template authoring surface plus transitional first-party execution host, and `FlowTime.TimeMachine` remains the E-18-owned replacement execution component. Forward-only cleanup deletes obsolete first-party endpoints outright rather than preserving 410, redirect, or advisory tombstone stubs. See `work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md` and `docs/architecture/supported-surfaces.md`.
**Consequences:** E-19 narrows current surfaces without redefining the long-term component split. Cleanup milestones cite the shared framing rather than inventing local boundary language.
