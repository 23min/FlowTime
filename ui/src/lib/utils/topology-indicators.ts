/**
 * Pure geometry helpers for the m-E21-07 topology warning indicators
 * (ACs 7 + 8). Shared by the imperative `$effect` in
 * `routes/time-travel/topology/+page.svelte` that injects warning dots into
 * the dag-map-rendered SVG after each render pass.
 *
 * Kept free of DOM types so vitest can run them in the project's `node`
 * environment (no jsdom). The consuming effect reads the rendered SVG with
 * `querySelector` + parses `cx`/`cy`/`r` numeric attributes, then calls these
 * helpers with plain numbers.
 *
 * Why two separate helpers: nodes carry their full geometry on the inner
 * `<circle data-id="...">` element (cx, cy, r). Edges are rendered as
 * `<path d="...">` elements where midpoint geometry must come from the live
 * SVG via `getPointAtLength(getTotalLength()/2)` — that DOM call belongs in
 * the effect, not here, but the offset / radius math for the resulting dot
 * is still useful to extract for testing.
 */

/**
 * Geometry of a node's main circle (the dag-map "station" circle, rendered
 * by `lib/dag-map/src/render.js` at `<circle data-id="...">`). All in SVG
 * user-space coordinates.
 */
export interface NodeCircleGeometry {
	cx: number;
	cy: number;
	r: number;
}

/**
 * Output position of the warning indicator dot — center coordinates plus
 * radius, all in SVG user-space coordinates. Consumed by the effect to
 * append a `<circle>` to the node's `<g data-node-id>` group.
 */
export interface IndicatorPosition {
	cx: number;
	cy: number;
	r: number;
}

/**
 * Place the node-warning dot at the top-right "shoulder" of the node
 * circle: 45 degrees NE from center, on the circle's edge. The dot's own
 * radius is 0.55x the node radius so the indicator stays proportional to
 * the rendered node size (dag-map node radii vary by interchange / depth /
 * scale; pinning a fixed pixel radius would make the dot oversize for tiny
 * nodes and undersize for large ones).
 *
 * Why top-right: the metric label sits above the node (`pos.y - r - 2*s`
 * in `lib/dag-map/src/render.js:301`); the node label sits below
 * (`pos.y + r + 8*s` at line 328). Top-right keeps the dot clear of both
 * label zones. The dag-map selection ring (when the node is pinned) sits at
 * radius `r + 3*s` per `render.js:294`; the dot at distance `r` from center
 * may visually overlap the ring's bottom-left arc, but the dot is appended
 * to the node group AFTER the ring is rendered so z-order keeps the dot on
 * top — and the warning + pinned states are both meaningful, not mutually
 * exclusive.
 */
export function nodeIndicatorPosition(
	circle: NodeCircleGeometry,
): IndicatorPosition {
	const offset = circle.r * 0.71; // ~cos(45°) — places dot center on the NE tangent
	return {
		cx: circle.cx + offset,
		cy: circle.cy - offset,
		r: circle.r * 0.55,
	};
}

/**
 * Place the edge-warning dot at the supplied midpoint. The midpoint itself
 * is computed in the consuming effect via `path.getPointAtLength(...)` —
 * that DOM call cannot run in the test environment, so this helper takes
 * pre-computed coordinates and only applies the dot-size convention.
 *
 * Edge dot radius is a fixed 3 user-space units rather than a fraction of
 * the path's stroke width: dag-map edge stroke thickness varies by route
 * count and `seg.thickness` (see `render.js:208`), and a stroke-relative
 * dot would shrink to invisibility on thin edges and balloon on thick
 * trunk edges. A constant size keeps the indicator readable across the
 * full layout.
 */
export function edgeIndicatorPosition(midpoint: {
	x: number;
	y: number;
}): IndicatorPosition {
	return {
		cx: midpoint.x,
		cy: midpoint.y,
		r: 3,
	};
}

/**
 * Build the `points` string for an SVG `<polygon>` rendering an upward-
 * pointing equilateral warning triangle centered at `(cx, cy)` with
 * circumradius `r`. The result is consumed directly by `polygon.setAttribute(
 * 'points', ...)`.
 *
 * Vertex math: top vertex on the y-axis at distance `r`; the two base
 * vertices form an equilateral triangle with the top, placed below the
 * center. `sin(60°) ≈ 0.866`, `cos(60°) = 0.5`.
 *
 * Why a triangle (not a circle) for the edge indicator: the universal
 * warning glyph reads sharply at small sizes thanks to its angular
 * silhouette; on a thin SVG path a small filled circle was barely
 * distinguishable from the path itself.
 */
export function triangleIndicatorPoints(geometry: IndicatorPosition): string {
	const { cx, cy, r } = geometry;
	const halfWidth = r * 0.866;
	const baseY = cy + r * 0.5;
	const topY = cy - r;
	return `${cx.toFixed(2)},${topY.toFixed(2)} ${(cx - halfWidth).toFixed(2)},${baseY.toFixed(2)} ${(cx + halfWidth).toFixed(2)},${baseY.toFixed(2)}`;
}

/**
 * Parse a numeric SVG attribute. Returns `null` when the attribute is
 * missing, blank, or not a finite number — the consuming effect skips the
 * indicator for that node rather than risk an `NaN` coordinate that would
 * silently corrupt the SVG.
 *
 * Extracted as a helper so the parse-and-validate dance is testable
 * without a DOM. The effect calls `circle.getAttribute('cx')` and feeds
 * the string straight in.
 */
export function parseSvgNumber(raw: string | null | undefined): number | null {
	if (raw === null || raw === undefined) return null;
	const trimmed = raw.trim();
	if (trimmed === '') return null;
	const value = Number(trimmed);
	if (!Number.isFinite(value)) return null;
	return value;
}
