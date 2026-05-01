---
id: D-009
title: Upgrade bits-ui to 2.16.5
status: accepted
---

**Status:** active
**Context:** bits-ui 2.15.0 was missing RadioGroup exports (empty `export {}`). bits-ui 2.16.4 had broken type exports. bits-ui 2.16.5 fixes both issues.
**Decision:** Upgrade bits-ui from 2.15.0 to 2.16.5. Remove the pin.
**Consequences:** RadioGroup (and other newer primitives) now available. shadcn-svelte radio-group component works correctly.
