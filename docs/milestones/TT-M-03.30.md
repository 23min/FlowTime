# TT‑M‑03.30 — UI Overlays for Retries + Service Time (Edges + Nodes)

Status: Planned  
Owners: UI + Platform (API contract consumers)  
References: docs/development/milestone-documentation-guide.md, docs/development/TEMPLATE-tracking.md

## Overview

Add first‑class visualizations for retries and service time. Color edges by retry heat, provide edge legends/toggles, and allow nodes to use Service Time as a color basis. Enhance inspector with per‑edge mini charts and link hover/selection.

## Goals

- Edge overlays: color by retry rate or attempts density; optional label with %, cap for readability.  
- Node color basis: allow “Service Time” in addition to existing bases; thresholds configurable.  
- Inspector ↔ canvas linking: hovering a dependency in inspector highlights the corresponding edge.

## Scope

In Scope
- Canvas: edge coloring modes (RetryRate, Attempts); legend, toggle in Feature Bar.  
- Canvas: node coloring basis adds ServiceTime.  
- Inspector: per‑edge retry mini chart + selection sync.  
- Persistence: store chosen overlay/basis in run state (local storage).

Out of Scope
- Advanced edge glyphs (arrows, multiple lanes).  
- Backoff pattern visualization (consider later).

## Requirements

### Functional
- RU1 — Edge overlay modes  
  - Toggle in `TopologyFeatureBar` with: Off | Retry Rate | Attempts.  
  - Colors pulled from `ColorScale` with a pastelized palette for overlays.  
  - Labels optional; truncation for dense graphs.
- RU2 — Node color basis: Service Time  
  - Add basis; thresholds managed by existing color settings structure.  
  - Tooltip includes S at current bin.
- RU3 — Linking  
  - Hover in inspector highlights edge; clicking scrolls/centers if off‑screen.  
  - Hover on canvas highlights corresponding inspector row.

### Non‑Functional
- Performance: overlays draw within existing render budgets; debounced on scrub.  
- Accessibility: sufficient contrast; text alternative for color cues.

## Deliverables
1) Feature Bar toggles for edge overlays and node basis.  
2) Canvas edge coloring implementation with legend.  
3) Inspector ↔ canvas link interactions.  
4) Docs: overlay modes, thresholds, examples.  
5) UI tests: toggle persistence, legend presence, highlight behaviors.

## Acceptance Criteria
- AC1: Switching edge overlay modes recolors edges and updates legend.  
- AC2: Node coloring by Service Time works; thresholds apply.  
- AC3: Inspector/canvas hover/selection are linked.  
- AC4: Settings persist across reloads.

## Implementation Plan (Sessions)

Session 1 — Toggles + State  
- Add toggles; add run state fields; persist to local storage.

Session 2 — Edge Coloring  
- Implement overlay renderer; sample from `state_window.edges` slice.  
- Legend + value clamping for readability.

Session 3 — Node Basis (Service Time)  
- Extend `ColorScale` and references to recognize ServiceTime basis.  
- Tooltip adds S value.

Session 4 — Linking + Tests  
- Hover interactions both ways; UI tests for toggles/legend/link.  
- Docs update with screenshots.

## Testing Strategy
- Unit: color mapping thresholds; settings persistence.  
- Integration: edge recolor on mode toggle; inspector/canvas linking.  
- Visual sanity via lightweight snapshot tests.

## Risks & Mitigations
- Visual clutter on dense graphs → allow Off mode; reduce label density; simplify legend.

## References
- docs/architecture/time-travel/ui-m3-roadmap.md  
- docs/development/milestone-documentation-guide.md

