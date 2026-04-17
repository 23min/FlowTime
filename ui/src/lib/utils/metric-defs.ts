/**
 * Metric definitions for the topology metric selector.
 *
 * Two shapes:
 *   - Snapshot (/state?binIndex=N): flat derived/metrics/byClass[cls] objects
 *   - Window (/state_window): series dict keyed by seriesKey, same per-class
 *
 * Each metric tracks both a snapshot path (for heatmap coloring) and a
 * series key (for sparklines from window data).
 */

export interface MetricDef {
	id: string;
	label: string;
	/** Dot-path into the snapshot node object, e.g. 'derived.utilization' */
	path: string;
	/** Series key in state_window NodeSeries.Series dict */
	seriesKey: string;
	/** How to format the value for display */
	format: (v: number) => string;
}

function pct(v: number): string {
	return `${Math.round(v * 100)}%`;
}

function num(v: number): string {
	if (Math.abs(v) >= 1000) return v.toFixed(0);
	if (Math.abs(v) >= 10) return v.toFixed(1);
	if (Math.abs(v) >= 1) return v.toFixed(2);
	return v.toFixed(3);
}

function ms(v: number): string {
	if (v >= 1000) return `${(v / 1000).toFixed(1)}s`;
	return `${v.toFixed(0)}ms`;
}

export const METRIC_DEFS: MetricDef[] = [
	{ id: 'utilization', label: 'Utilization', path: 'derived.utilization', seriesKey: 'utilization', format: pct },
	{ id: 'queue', label: 'Queue', path: 'metrics.queue', seriesKey: 'queue', format: num },
	{ id: 'arrivals', label: 'Arrivals', path: 'metrics.arrivals', seriesKey: 'arrivals', format: num },
	{ id: 'served', label: 'Served', path: 'metrics.served', seriesKey: 'served', format: num },
	{ id: 'errors', label: 'Errors', path: 'metrics.errors', seriesKey: 'errors', format: num },
	{ id: 'flowLatency', label: 'Latency', path: 'derived.flowLatencyMs', seriesKey: 'flowLatencyMs', format: ms },
];

export const DEFAULT_METRIC = METRIC_DEFS[0];

/**
 * Extract a numeric value from an object using a dot-path.
 * Returns undefined if the path doesn't resolve to a finite number.
 */
export function extractMetricValue(node: Record<string, unknown>, path: string): number | undefined {
	const parts = path.split('.');
	let current: unknown = node;
	for (const part of parts) {
		if (current === null || current === undefined || typeof current !== 'object') return undefined;
		current = (current as Record<string, unknown>)[part];
	}
	const n = Number(current);
	return isFinite(n) ? n : undefined;
}

/**
 * Build a metric map from snapshot state nodes for a given metric definition.
 * Returns a Map<nodeId, { value, label }> suitable for DagMapView.
 */
export function buildMetricMapForDef(
	nodes: Record<string, unknown>[],
	def: MetricDef
): Map<string, { value: number; label: string }> {
	return buildMetricMapForDefFiltered(nodes, def, new Set());
}

/**
 * Discover class IDs present in state data.
 * Scans all nodes' byClass entries for unique class keys.
 */
export function discoverClasses(nodes: Record<string, unknown>[]): string[] {
	const classes = new Set<string>();
	for (const node of nodes) {
		const byClass = node.byClass as Record<string, unknown> | undefined;
		if (byClass) {
			for (const key of Object.keys(byClass)) {
				classes.add(key);
			}
		}
	}
	return Array.from(classes).sort();
}

/**
 * Extract a snapshot metric value from a node, optionally filtered by active classes.
 * When activeClasses is non-empty, aggregate from byClass[cls].<field>.
 * When activeClasses is empty, use the aggregate value at the node level.
 *
 * The path shape for snapshot byClass is different from the top level:
 *   top-level: derived.utilization, metrics.queue
 *   byClass:   byClass[cls].utilization (flat, no nested derived/metrics wrapper)
 *
 * We translate paths: strip 'derived.' or 'metrics.' prefix when reading from byClass.
 */
export function extractMetricValueFiltered(
	node: Record<string, unknown>,
	path: string,
	activeClasses: Set<string>
): number | undefined {
	if (activeClasses.size === 0) {
		return extractMetricValue(node, path);
	}

	const byClass = node.byClass as Record<string, unknown> | undefined;
	if (!byClass) return undefined;

	// Strip leading derived./metrics. to get the flat key used in ClassMetrics
	const flatKey = path.replace(/^(derived|metrics)\./, '');

	let sum = 0;
	let haveAny = false;
	for (const cls of activeClasses) {
		const classData = byClass[cls] as Record<string, unknown> | undefined;
		if (!classData) continue;
		const raw = classData[flatKey];
		const n = Number(raw);
		if (raw !== undefined && raw !== null && isFinite(n)) {
			sum += n;
			haveAny = true;
		}
	}
	return haveAny ? sum : undefined;
}

/**
 * Build a snapshot metric map honoring an active class filter.
 */
export function buildMetricMapForDefFiltered(
	nodes: Record<string, unknown>[],
	def: MetricDef,
	activeClasses: Set<string>
): Map<string, { value: number; label: string }> {
	const m = new Map<string, { value: number; label: string }>();
	for (const node of nodes) {
		const id = node.id as string;
		if (!id) continue;
		const v = extractMetricValueFiltered(node, def.path, activeClasses);
		if (v !== undefined) {
			m.set(id, { value: v, label: def.format(v) });
		}
	}
	return m;
}

/**
 * Build per-node sparkline values from a state_window response's NodeSeries array.
 *
 * windowNodes shape: Array<{ id, series: { [key]: (number|null)[] }, byClass?: {...} }>
 * When activeClasses is non-empty, sum per-class series across active classes.
 */
export function buildSparklineSeries(
	windowNodes: Record<string, unknown>[],
	def: MetricDef,
	activeClasses: Set<string>
): Map<string, number[]> {
	const result = new Map<string, number[]>();

	for (const node of windowNodes) {
		const id = node.id as string;
		if (!id) continue;

		const values = extractSeriesValues(node, def.seriesKey, activeClasses);
		if (values && values.length > 0) {
			result.set(id, values);
		}
	}

	return result;
}

function extractSeriesValues(
	node: Record<string, unknown>,
	seriesKey: string,
	activeClasses: Set<string>
): number[] | undefined {
	if (activeClasses.size === 0) {
		const series = node.series as Record<string, unknown> | undefined;
		if (!series) return undefined;
		const arr = series[seriesKey];
		if (!Array.isArray(arr)) return undefined;
		return (arr as (number | null)[]).map((v) => (v === null || v === undefined ? NaN : Number(v)));
	}

	const byClass = node.byClass as Record<string, unknown> | undefined;
	if (!byClass) return undefined;

	// Sum across active classes
	let result: number[] | undefined;
	for (const cls of activeClasses) {
		const classData = byClass[cls] as Record<string, unknown> | undefined;
		if (!classData) continue;
		const arr = classData[seriesKey];
		if (!Array.isArray(arr)) continue;
		const values = (arr as (number | null)[]).map((v) =>
			v === null || v === undefined ? NaN : Number(v)
		);
		if (!result) {
			result = values;
		} else {
			for (let i = 0; i < result.length && i < values.length; i++) {
				const a = result[i];
				const b = values[i];
				if (isFinite(a) && isFinite(b)) result[i] = a + b;
				else if (isFinite(b)) result[i] = b;
				// else leave a (may be NaN)
			}
		}
	}
	return result;
}
