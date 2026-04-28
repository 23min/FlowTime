/**
 * Pure label-assembly helpers for the m-E21-08 topology a11y retrofit
 * (AC1). Shared by the imperative `$effect` in
 * `routes/time-travel/topology/+page.svelte` that walks the dag-map-rendered
 * SVG after each render pass and applies `tabindex` / `role` / `aria-label`
 * to nodes and edges so the topology surface reaches Blazor-parity
 * keyboard + screen-reader bar.
 *
 * Free of DOM types so vitest can run them in `node` environment. The
 * consuming effect reads node ids / edge identities / metric values from
 * the rendered SVG and derived stores, then calls these helpers with
 * plain values.
 *
 * Why the strings are anchored here rather than authored inline in the
 * effect: future-author drift. A test catches a missing fallback or a
 * label-format change long before a Playwright spec would.
 */

export interface NodeAriaLabelInput {
	nodeId: string;
	/** Class label (e.g. "cust", "wip") when known; null/undefined/empty when not. */
	className?: string | null;
	/** Human-readable metric label (e.g. "Utilization"). */
	metricLabel: string;
	/** Current metric value for the node, or null/undefined/NaN when not available. */
	metricValue?: number | null;
}

/**
 * Compose an `aria-label` for a topology node.
 *
 * Shape: `"<nodeId> (<className>) — <metricLabel>: <metricValue>"`.
 *
 * Fallbacks:
 * - Class unknown (null / undefined / empty string) → omit the parenthesised segment.
 * - Metric value missing (null / undefined / NaN) → `"<metricLabel>: no data"`.
 *
 * Numeric values render with two-decimal precision (`toFixed(2)`) — matches
 * the workbench-card metric-row precision and keeps the screen-reader
 * readout short.
 */
export function buildNodeAriaLabel(input: NodeAriaLabelInput): string {
	const { nodeId, className, metricLabel, metricValue } = input;

	const classSegment = className && className.length > 0 ? ` (${className})` : '';
	const valueSegment =
		typeof metricValue === 'number' && Number.isFinite(metricValue)
			? metricValue.toFixed(2)
			: 'no data';

	return `${nodeId}${classSegment} — ${metricLabel}: ${valueSegment}`;
}

export interface EdgeAriaLabelInput {
	from: string;
	to: string;
}

/**
 * Compose an `aria-label` for a topology edge.
 *
 * Shape: `"edge from <from> to <to>"`. Dotted node ids are preserved
 * verbatim — the screen-reader announces the literal id.
 *
 * Edge metric values are not in the dag-map render path today; if a
 * future milestone surfaces them on edges, extend this helper rather
 * than authoring parallel string assembly inline.
 */
export function buildEdgeAriaLabel(input: EdgeAriaLabelInput): string {
	return `edge from ${input.from} to ${input.to}`;
}
