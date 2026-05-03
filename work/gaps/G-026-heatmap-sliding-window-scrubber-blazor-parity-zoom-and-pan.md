---
id: G-026
title: Heatmap sliding-window scrubber (Blazor-parity zoom-and-pan)
status: open
---

### Why this is a gap

M-043 Heatmap View ships a **fit-to-width** toggle (`ft.heatmap.fitWidth`, default off) that compresses `CELL_W` to `max(3, min(18, floor(containerWidth / binCount)))` so wide runs (e.g. 288 bins for the multi-tier supply chain model) fit the viewport without horizontal scroll. That solves the overview-first case but sacrifices per-cell fidelity — at 3–5 px per bin, individual tile values are hard to read, tooltip hover is fiddly, and the column-highlight marker shrinks accordingly.

The Blazor UI's scrubber has a **draggable window** affordance — a resizable/pannable range on the scrubber track that selects a subset of bins for inspection. The Svelte timeline scrubber currently only exposes a single-thumb `currentBin`. A Blazor-parity dual-handle "window scrubber" would let users keep the default 18 px cell size at full fidelity while panning across a long run:

- Window size = e.g. 64 bins; drag the window across 288 bins = five screens of detail.
- Heatmap renders `binCount=64` with `CELL_W=18`, no compression.
- The scrubber track doubles as a minimap-style summary.
- `state_window` already accepts `startBin` / `endBin`, so the data plane is ready.

### Status

Open, deferred by user decision 2026-04-24. Fit-to-width is the 80 % solution for overview; the sliding window is the 80 % solution for detail-at-scale. Shipping the window properly needs a dedicated milestone because:

- `TimelineScrubber` needs dual handles (window-start, window-end) plus a window body for drag-to-pan.
- `currentBin` vs `windowBin` semantics must be nailed down (what does "pin this cell" mean when the user is viewing bins 128–191 but the full-run thumb is at 30?).
- Heatmap and topology both need to consume the window range from the shared view-state store (new `windowRange` field).
- Playwright coverage for drag-pan, resize, and keyboard equivalents (PgUp / PgDn to pan).

### Immediate implications

- Until the sliding-window milestone lands, fit-to-width is the only knob for wide runs on the heatmap.
- Plan for a new E-21 milestone after M-044 Validation and M-045 Polish — or fold into polish if scope allows.
- When the milestone lands, the Blazor-parity gesture needs to be muscle-memory-compatible for existing users (drag the window body; resize via handles).

---
