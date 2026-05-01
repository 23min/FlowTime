---
id: D-037
title: Stored drafts are retired; only the inline run path may survive temporarily
status: accepted
---

**Status:** active
**Context:** Stored draft CRUD has no supported first-party UI caller. The only live reason to keep `drafts/run` is the explicit inline-YAML “run this now” path.
**Decision:** Retire stored drafts entirely in M-025. Delete `/api/v1/drafts` CRUD, `StorageKind.Draft`, and `data/storage/drafts/`. Keep only the inline-source `POST /api/v1/drafts/run` path until the Time Machine replacement is ready.
**Consequences:** Draft persistence is no longer treated as a product promise. If model versioning is wanted later, it must be designed against compiled/runtime identity rather than resurrecting stored drafts.
