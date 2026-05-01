---
id: D-011
title: dag-map work scoped within consuming epics
status: accepted
---

**Status:** active
**Context:** dag-map is a cross-cutting library that multiple epics need (path highlighting for Path Analysis, edge coloring for Inspector, constraint visualization for Dependency Constraints). Considered making it a separate epic.
**Decision:** dag-map enhancements are scoped as deliverables within the consuming epic's milestones, not a separate epic. Same pattern as M4 pulling in "dag-map heatmap mode."
**Consequences:** Each epic that needs dag-map features includes them in its milestone specs. No separate dag-map epic or backlog.
