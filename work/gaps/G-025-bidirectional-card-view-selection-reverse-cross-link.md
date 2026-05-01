---
id: G-025
title: Bidirectional card ↔ view selection (reverse cross-link)
status: open
---

### Why this is a gap

M-043 Heatmap View ships a **one-way** cross-link: clicking a heatmap cell or a topology node pins the node and sets `viewState.selectedCell`; the matching workbench card's title renders in turquoise (`--ft-highlight`). The reverse path — click a workbench card → the corresponding cell in the heatmap and node in topology light up as the selected item — is not implemented.

Today, clicking a card body does nothing (only the ✕ close button is interactive). That's fine for shipping M-043, but it's the natural other half of the "same model, multiple views" principle the milestone is built on. For long pin-stacks it's common to wonder "where is this node in the graph?" without needing to hover-scan the heatmap or DAG.

### Proposed shape

- **Card body click** → `viewState.setSelectedCell(card.nodeId, viewState.currentBin)`.
  - Heatmap: the existing `selectedCell`-driven overlay rect automatically appears at (nodeId, currentBin). Zero new code in heatmap.
  - Topology: dag-map nodes are rendered as SVG via `{@html renderSVG(...)}`. Add a CSS rule `.node-selected { stroke: var(--ft-highlight); stroke-width: 2; }` and a `$effect` that toggles the class by id — same pattern as the existing `.edge-selected` toggle in `ui/src/routes/time-travel/topology/+page.svelte:127–128`.
- Keep the ✕ close button as the sole unpin surface on the card. Card body click = select only (no toggle / no unpin).
- Keyboard reachability on the card body for a11y (space / enter → same effect).

### Status

Open, captured 2026-04-24 as a "natural next step" after M-043 card cross-link work.

### Immediate implications

- Bundle into **M-045 Polish** — that milestone already has topology keyboard-nav + ARIA retrofit and color-blind validation queued. Adding the reverse-cross-link while topology SVG is being touched is cheap.
- Before shipping: decide whether card body click conflicts with any future card interactivity (expand/collapse, drill-in). If so, scope the click to the card title bar rather than the whole body — leaves the body area free for subsequent interactive content.
- No backend changes needed.

### Reference

- `ui/src/routes/time-travel/topology/+page.svelte:127-128` — existing `.edge-selected` class toggle pattern.
- `ui/src/lib/stores/view-state.svelte.ts` — `setSelectedCell` / `clearSelectedCell` already in place.
- `ui/src/lib/components/workbench-card.svelte` — needs a click handler on the card body or title.

---
