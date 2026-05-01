---
id: G-024
title: Data-viz palette not validated for color-blindness (m-E21-06 AC12 homework)
status: open
---

### Why this is a gap

The `--ft-viz-*` palette introduced in M-039 (teal, pink, coral, blue, green, amber, purple) was chosen for general aesthetic contrast but was not validated against color-blindness simulators. Under ADR-m-E21-06-02 both topology and heatmap now share the same teal → amber → red gradient from `dag-map`'s `colorScales.palette`, so the issue amplifies — users with deuteranopia or protanopia may see low-utilization teal and high-utilization red as the same muted hue.

### Status

Open. Deferred from M-043 per confirmation #3 — pattern-encoding (redundant hatch overlay) and high-contrast tuning land in M-045 polish milestone.

### Immediate implications

- Until polish lands, users with color-vision differences will rely on the `data-value-bucket` attribute semantics (`low` / `mid` / `high`) and on hover tooltips for correctness.
- When M-045 runs, add a deuteranopia / protanopia / tritanopia simulator pass to the workbench smoke test; pattern-encode heatmap cells when enabled via a user preference toggle.

---
