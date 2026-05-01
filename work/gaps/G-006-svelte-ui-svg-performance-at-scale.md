---
id: G-006
title: 'Svelte UI: SVG Performance at Scale'
status: open
---

The “all dependencies” non-operational view has ~60-80 nodes and 200+ edges. SVG should handle this (est. ~600 DOM elements), but hasn't been tested. If it struggles:
- Try DOM-based metric updates (setAttribute) instead of full SVG re-render
- Consider canvas hybrid (dag-map for layout, canvas for rendering) as last resort
- Semantic zoom (dot → station → card at zoom levels) could reduce element count

---
