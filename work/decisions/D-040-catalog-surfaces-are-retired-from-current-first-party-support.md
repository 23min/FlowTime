---
id: D-040
title: Catalog surfaces are retired from current first-party support
status: accepted
---

**Status:** active
**Context:** Catalog endpoints and UI helpers survive mostly as mock/default residue. Active first-party callers already behave as if catalogs are not used.
**Decision:** Delete catalog endpoints, services, UI selectors, placeholder `catalogId` plumbing, and `data/catalogs/` usage in M-025.
**Consequences:** No current first-party caller is allowed to rely on catalogs. If catalog-like behavior is wanted later, it must be redesigned from a real use case rather than preserved through zombie endpoints.
