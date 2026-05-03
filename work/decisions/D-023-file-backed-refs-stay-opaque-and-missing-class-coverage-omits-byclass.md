---
id: D-023
title: File-backed refs stay opaque and missing class coverage omits byClass
status: accepted
---

**Status:** active
**Context:** E-16 review found two boundary leaks still active after typed semantic references landed: runtime code inferred producer node identity from file stems, and state projection synthesized wildcard `byClass` payloads from aggregate totals even when runs had no explicit class series. Both behaviors blurred the compiler/runtime boundary and made missing class coverage indistinguishable from explicit fallback coverage.
**Decision:** File-backed compiled references remain opaque at runtime: they provide authored lookup keys, not producer node IDs. State and graph projection only emit wildcard `byClass` when explicit fallback class series exist; runs with missing class coverage omit `byClass` entirely.
**Consequences:** Logical-type promotion, queue-origin checks, and graph dependency resolution cannot rely on file-name heuristics. Tests and approved snapshots that depended on implicit wildcard totals or file-stem inference must be regenerated forward-only.
