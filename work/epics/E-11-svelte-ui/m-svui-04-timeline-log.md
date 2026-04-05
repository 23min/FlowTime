# Tracking: m-svui-04-timeline

**Started:** 2026-03-30
**Status:** partial (basics done, full AC not yet met)
**Commits:** `19cea20` (timeline + heatmap), dag-map `44d254b` (heatmap), `7ff4c41` (lineGap)

## Progress

| AC | Status | Notes |
|----|--------|-------|
| Dragging timeline scrubs bins and updates canvas | partial | Scrubber works, heatmap colors update, but model has constant utilization so no visual change per bin |
| Play button auto-advances bins | done | 500ms interval with loop |
| Loop wraps around at end | done | Resets to bin 0 |
| Warning chips appear for current window | not started | |
| Changing focus metric updates color overlay | not started | Only utilization mapped so far |

## Decisions

- Used dag-map heatmap mode (metrics Map + colorScale) instead of DOM attribute updates
- Layout cached separately from render — metric changes don't recompute positions
- State API `derived.utilization` is the primary metric; `derived.throughputRatio` as fallback
- lineGap defaults to 0 for auto-discovered routes (dag-map library fix)

## Known Issues

- Timeline doesn't show timestamps or bin labels (just index)
- No speed selector chips (0.5x, 1x, 2x, 4x)
- No focus metric chips (SLA, Utilization, Error rate, etc.)
- No warning chips
- Constant-utilization models show no color change across bins
