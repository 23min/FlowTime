---
id: D-0021
title: Shared runtime parameter foundation is owned once and reused by E-0017 and E-0018
status: accepted
---

**Status:** active
**Context:** E-0017 (Interactive What-If) and E-0018 (Headless Pipeline & Optimization) both need the same foundational capability: identify editable parameters in compiled graphs, apply deterministic overrides without recompilation, and expose metadata suitable for either UI controls or programmatic callers. Duplicating that work across two future epics would create drift between the interactive and headless paths.
**Decision:** Own the shared runtime parameter foundation once in the programmable/headless layer. The foundation includes compiled parameter identity, override points, reevaluation APIs, and the contract for enriching human-facing metadata from authored template parameters when available. E-0017 consumes that foundation for sessions, push delivery, and UI controls rather than defining a second runtime parameter model.
**Consequences:** E-0018 foundation work (`m-E18-01/02`) precedes or runs alongside the UI/session-specific work in E-0017. Reviews should treat any second, UI-only runtime parameter model as a regression.
