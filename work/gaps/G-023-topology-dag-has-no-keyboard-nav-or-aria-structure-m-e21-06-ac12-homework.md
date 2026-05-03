---
id: G-023
title: Topology DAG has no keyboard nav or ARIA structure (m-E21-06 AC12 homework)
status: open
---

### Why this is a gap

M-043 establishes the accessibility bar for the Svelte workbench: heatmap cells are keyboard-reachable via Tab + arrow keys, carry `role="grid"`/`role="row"`/`role="gridcell"` with `aria-label` containing node id + bin + metric + value, render a visible focus ring, and fire tooltip-on-focus. During the AC12 homework audit the topology DAG area was found to lag that bar:

- DAG SVG is rendered via `{@html renderSVG(...)}` from the dag-map library; nodes have no `tabindex` and cannot be reached by keyboard.
- No ARIA roles on the SVG container, nodes, or edges — a screen-reader user gets no structure.
- Node-click is the only input modality.
- Edge interaction (click-to-pin-edge) has no keyboard equivalent.

Per milestone confirmation #3, this was not retrofitted inside M-043. The heatmap ships at the higher bar; topology remains at the earlier bar.

### Status

Open. Blazor UI's original topology had keyboard + ARIA; Svelte topology regressed here and the regression predates M-043.

### Immediate implications

- Do not ship accessibility audits against the Svelte workbench as "complete" until topology reaches heatmap's bar.
- When a future milestone (likely M-045 polish) adds pattern encoding / high-contrast tuning, include topology keyboard + ARIA retrofit in the same pass.
- The dag-map library itself may need to grow tabindex / role options on its rendered nodes; coordinate with the library owners before forking rendering in the topology Svelte component.

---
