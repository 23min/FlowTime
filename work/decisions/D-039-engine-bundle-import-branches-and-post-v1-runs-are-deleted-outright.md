---
id: D-039
title: Engine bundle-import branches and POST /v1/runs are deleted outright
status: accepted
---

**Status:** active
**Context:** `POST /v1/runs` still carries bundle-import branches that only tests exercise. Preserving the route only to return a rejection stub would still keep a legacy endpoint alive for advisory purposes, which conflicts with the repo's forward-only cleanup rule.
**Decision:** Delete bundle-import branches from `POST /v1/runs` in M-025 and delete the route itself. No 410-style rejection stub is retained. More generally, E-19 cleanup milestones do not preserve obsolete first-party endpoints solely to tell callers where behavior moved.
**Consequences:** Current runtime ownership becomes clearer because dead routes disappear instead of lingering as migration hints. If cross-environment import returns later, it must come back as an explicitly designed Time Machine concern instead of surviving as unowned residue.
