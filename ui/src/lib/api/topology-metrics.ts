// Pure helpers for mapping engine series onto the topology graph.
//
// Produces the `metrics` map that dag-map-view consumes for heatmap overlay.
// Extracted so it can be unit-tested without DOM.

import type { EngineGraph } from './engine-session.js';
import { isCompilerTemp, isPerClassSeries } from '../utils/format.js';

/**
 * True if a series should NOT appear as a metric option in the heatmap dropdown.
 * Metric options are base (non-per-class) user-visible series only.
 */
function isNonMetric(name: string): boolean {
	return isCompilerTemp(name) || isPerClassSeries(name);
}

export interface NodeMetric {
	value: number;
	label?: string;
}

export type MetricMap = Map<string, NodeMetric>;

/**
 * Pick a default metric to drive the heatmap:
 * returns the first non-internal series name, or null if none.
 */
export function defaultMetric(series: Record<string, number[]>): string | null {
	for (const name of Object.keys(series)) {
		if (!isNonMetric(name)) return name;
	}
	return null;
}

/**
 * List all non-internal series names, for the metric dropdown.
 */
export function availableMetrics(series: Record<string, number[]>): string[] {
	return Object.keys(series).filter((name) => !isNonMetric(name));
}

/**
 * Aggregate a series to a single scalar: mean of all bins.
 * Returns 0 for empty series.
 */
export function seriesMean(values: number[]): number {
	if (values.length === 0) return 0;
	let sum = 0;
	for (const v of values) sum += v;
	return sum / values.length;
}

/**
 * Pick a single scalar from a series.
 * When `bin` is a valid index, returns `values[bin]`.
 * Otherwise (undefined, negative, or out of range) falls back to the mean.
 */
function pickValue(values: number[], bin?: number): number {
	if (bin !== undefined && bin >= 0 && bin < values.length) {
		return values[bin];
	}
	return seriesMean(values);
}

/**
 * Build the metrics map consumed by dag-map-view.
 *
 * For each graph node, use the node's primary series as the metric value.
 * When `bin` is provided and in range, uses the value at that bin (scrubber
 * mode); otherwise uses the mean across all bins (default / mean mode).
 *
 * Primary series lookup order:
 *   1. series[node.id]          — matches const/expr nodes named identically
 *   2. series[snake(id)_queue]  — topology queue synthesized column
 *   3. omit from the map        — node renders with default dag-map base color
 *
 * Values are raw — call `normalizeMetricMap` before passing to dag-map-view.
 */
export function buildMetricMap(
	graph: EngineGraph,
	series: Record<string, number[]>,
	bin?: number,
): MetricMap {
	const map = new Map<string, NodeMetric>();

	for (const node of graph.nodes) {
		const values = findNodeSeries(node.id, series);
		if (values === undefined) continue;
		map.set(node.id, { value: pickValue(values, bin), label: node.id });
	}
	return map;
}

/**
 * Build the edge metrics map consumed by dag-map-view's edgeMetrics prop.
 *
 * For each edge {from, to}, the flow value is the from-node's primary series.
 * When `bin` is provided and in range, uses the value at that bin (scrubber
 * mode); otherwise uses the mean across all bins (default / mean mode).
 *
 * Key format: `${fromId}\u2192${toId}` — the Unicode right-arrow character (→),
 * confirmed from dag-map/src/render.js:151: `\`${fromId}\u2192${toId}\``
 *
 * Values are raw — call normalizeMetricMap before passing to dag-map-view.
 */
export function buildEdgeMetricMap(
	graph: EngineGraph,
	series: Record<string, number[]>,
	bin?: number,
): MetricMap {
	const map = new Map<string, NodeMetric>();

	for (const edge of graph.edges) {
		const values = findNodeSeries(edge.from, series);
		if (values === undefined) continue;
		const key = `${edge.from}\u2192${edge.to}`;
		map.set(key, { value: pickValue(values, bin), label: edge.from });
	}
	return map;
}

/**
 * Find the primary series for a graph node, trying multiple naming conventions.
 * Returns undefined if no match is found.
 */
function findNodeSeries(
	nodeId: string,
	series: Record<string, number[]>,
): number[] | undefined {
	// 1. Exact match (const, expr, router, pmf nodes)
	if (series[nodeId] !== undefined) return series[nodeId];

	// 2. Topology queue column: `{snake_case(id)}_queue`
	const snake = toSnakeCase(nodeId);
	const queueKey = `${snake}_queue`;
	if (series[queueKey] !== undefined) return series[queueKey];

	return undefined;
}

/**
 * Convert CamelCase or PascalCase to snake_case. Matches the Rust compiler's
 * `to_snake_case` helper so the key format aligns with `queue_column_id`.
 *
 * Insert an underscore before an uppercase letter when the previous char was
 * lowercase, OR when the previous char was uppercase but the next char is
 * lowercase (end of acronym).
 *
 * Examples:
 *   "Queue" → "queue"
 *   "MyQueue" → "my_queue"
 *   "ServiceA" → "service_a"
 *   "XMLParser" → "xml_parser"
 */
function toSnakeCase(id: string): string {
	if (id.length === 0) return '';
	const out: string[] = [];
	const isUpper = (c: string) => c >= 'A' && c <= 'Z';
	const isLower = (c: string) => c >= 'a' && c <= 'z';
	for (let i = 0; i < id.length; i++) {
		const ch = id[i];
		if (isUpper(ch) && i > 0) {
			const prev = id[i - 1];
			const next = i + 1 < id.length ? id[i + 1] : '';
			const prevUpper = isUpper(prev);
			const nextLower = isLower(next);
			if (!prevUpper || nextLower) {
				out.push('_');
			}
		}
		out.push(ch.toLowerCase());
	}
	return out.join('');
}

/**
 * Normalize a metric map's values to [0, 1] for use with dag-map's colorScale,
 * which expects normalized input. Returns a new Map — does not mutate the input.
 *
 * - Values are linearly scaled: v → (v - min) / (max - min)
 * - When all values are equal (max === min), every node gets 0.5 (middle of scale)
 * - When the map is empty, returns an empty Map
 * - The `label` field preserves the raw value formatted as text for tooltip use
 */
export function normalizeMetricMap(map: MetricMap): MetricMap {
	if (map.size === 0) return new Map();

	let min = Infinity;
	let max = -Infinity;
	for (const { value } of map.values()) {
		if (value < min) min = value;
		if (value > max) max = value;
	}

	const normalized = new Map<string, NodeMetric>();
	const range = max - min;

	if (range === 0 || !Number.isFinite(range)) {
		// All values equal — place every node mid-scale
		for (const [key, entry] of map) {
			normalized.set(key, {
				value: 0.5,
				label: entry.label ?? formatRawValue(entry.value),
			});
		}
		return normalized;
	}

	for (const [key, entry] of map) {
		const t = (entry.value - min) / range;
		normalized.set(key, {
			value: t,
			label: entry.label ?? formatRawValue(entry.value),
		});
	}
	return normalized;
}

function formatRawValue(v: number): string {
	if (Number.isInteger(v)) return v.toString();
	return v.toFixed(2);
}

/**
 * Compute global min/max across all non-internal series — used for a shared
 * color scale across the heatmap so colors are comparable across nodes.
 */
export function seriesRange(series: Record<string, number[]>): { min: number; max: number } {
	let min = Infinity;
	let max = -Infinity;
	for (const [name, values] of Object.entries(series)) {
		if (isNonMetric(name)) continue;
		for (const v of values) {
			if (v < min) min = v;
			if (v > max) max = v;
		}
	}
	if (!Number.isFinite(min) || !Number.isFinite(max)) {
		return { min: 0, max: 1 };
	}
	return { min, max };
}
