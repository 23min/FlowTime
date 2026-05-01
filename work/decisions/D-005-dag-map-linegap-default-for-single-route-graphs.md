---
id: D-005
title: dag-map lineGap default for single-route graphs
status: accepted
---

**Status:** active
**Context:** dag-map's lineGap (parallel line offset at shared nodes) defaults to 5px. For auto-discovered routes, this causes the trunk to wobble even when there's only one visual route.
**Decision:** Default lineGap to 0 when routes are auto-discovered (not consumer-provided). Only use non-zero lineGap when consumer explicitly provides multiple routes.
**Consequences:** Single-route graphs render with straight trunks. Multi-route flow layouts still get parallel line separation.
