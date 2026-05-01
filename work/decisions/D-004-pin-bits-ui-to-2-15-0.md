---
id: D-004
title: Pin bits-ui to 2.15.0
status: superseded
---

**Status:** superseded by D-009
**Context:** bits-ui 2.16.4 has broken dist/types.js — references `../bits/pin-input/pin-input.svelte.js` and `./attributes.js` which don't exist in the published package.
**Decision:** Pin bits-ui to 2.15.0 until the issue is fixed upstream.
**Consequences:** Check for fix on bits-ui releases periodically. Can unpin when 2.16.5+ ships.
