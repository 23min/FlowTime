---
id: D-041
title: Blazor and Svelte stay parallel supported UIs without parity requirements
status: accepted
---

**Status:** active
**Context:** E-19 needed an explicit UI support policy so cleanup work would not silently turn into Blazor retirement or parity gating.
**Decision:** Keep Blazor and Svelte as parallel supported first-party UIs. Shared contract changes must keep both functional, but feature parity is not required. Blazor-specific debugging/operator workflows remain supported, and cleanup work should remove stale wrappers rather than supported capabilities.
**Consequences:** M-027 focuses on stale-wrapper removal and contract alignment, not UI retirement. Svelte is not blocked on missing Blazor features, and Blazor is not required to preserve deprecated compatibility paths.
