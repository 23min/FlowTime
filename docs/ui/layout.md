# Topology Layout Requirements

This document captures the constraints we rely on when computing the "happy path" layout for the time-travel topology canvas. Any future layout iteration—custom or third-party—needs to satisfy these rules.

## Grid & Lanes
- We render against a fixed grid of five vertical lanes: two left support lanes, the center backbone lane, and two right lanes (the far-right is dedicated to scalar/leaf nodes).
- Operational/service nodes must stay in the three backbone lanes; supporting const/expr nodes use the left lanes; scalar leaves use the rightmost lane.
- Every lane is a strict stack of discrete rows. No two nodes—of any kind—may share the same `(lane, row)` cell.

## Flow Direction & Rows
- Flow is always **top → bottom**. For every edge we enforce `child.row ≥ parent.row + 1`.
- Service → service edges should connect vertically adjacent rows (no blank row between parent and child when they share a lane).
- Supporting nodes that feed a service must render **above** their downstream target. Multi-tier support stacks still obey this one-row separation all the way back to their ultimate service.
- Leaf nodes (const/expr with no outputs) render immediately **below** their parents, using the minimum number of extra rows required by collisions; they should never sink to the bottom of the canvas without cause.

## Spacing & Overlap Prevention
- Overlap is forbidden. Every node must end up with a unique `(lane, row)` pair after all layout passes.
- When a lane overfills, re-stack it top-to-bottom: respect each node’s parent baseline, enforce the one-row clearance above children, and keep service chains contiguous.
- Supporting nodes in the left lanes should maintain a small vertical gap from their parents so their curved “C” edges stay short and readable.

## Edge Rendering Expectations
- Same-lane service edges are straight vertical lines when nodes are vertically adjacent; bezier curves only appear when lanes differ.
- Supporting edges (const/expr → service) still use bezier “squished C” arcs, but their tangents must start/end horizontally and touch the node boundary (no gaps).
- Leaf edges also curve gently into the backbone, using the shortest possible path because the leaf rows sit right beneath their parents.

## Mode & Input Assumptions
- The layout recomputation only runs when the overlay has `Layout = Happy Path` **and** `Respect model coordinates = false`. If UI positions are respected we must render verbatim coordinates.
- Switching between `Happy Path` and `Layered` must rebuild the layout from scratch, including clearing cached viewport state, to avoid infinite render loops.

## Interaction Expectations (UI polish)
- Selecting a node shows the properties icon beside it; Enter or clicking the icon opens the properties panel. Clicking the node alone should not auto-open the panel.
- Tooltips appear on hover (not on focus regain), live until their timer expires even if the node becomes selected, and stay suppressed once the user dismisses them.

Keep this checklist handy when experimenting with different layout strategies (e.g., Sugiyama/Dagre/elkjs). If a candidate algorithm cannot meet every requirement above, document the trade-offs before adopting it.
