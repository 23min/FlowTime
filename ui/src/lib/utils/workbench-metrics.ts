/**
 * Extract workbench card metrics from a state API node response.
 */

export interface CardMetric {
	label: string;
	value: string;
}

/** Format a number for compact display. */
function fmt(v: unknown): string {
	if (v === null || v === undefined) return '-';
	const n = Number(v);
	if (!isFinite(n)) return '-';
	if (Math.abs(n) >= 1000) return n.toFixed(0);
	if (Math.abs(n) >= 10) return n.toFixed(1);
	if (Math.abs(n) >= 1) return n.toFixed(2);
	return n.toFixed(3);
}

function pct(v: unknown): string {
	if (v === null || v === undefined) return '-';
	const n = Number(v);
	if (!isFinite(n)) return '-';
	return `${(n * 100).toFixed(1)}%`;
}

/**
 * Extract displayable metrics from a state API node object.
 * Returns label/value pairs for the workbench card.
 */
export function extractNodeMetrics(node: Record<string, unknown>): CardMetric[] {
	const result: CardMetric[] = [];
	const metrics = node.metrics as Record<string, unknown> | undefined;
	const derived = node.derived as Record<string, unknown> | undefined;

	if (derived?.utilization !== undefined && derived.utilization !== null && isFinite(Number(derived.utilization))) {
		result.push({ label: 'Utilization', value: pct(derived.utilization) });
	}

	if (metrics?.queueDepth !== undefined && metrics.queueDepth !== null) {
		result.push({ label: 'Queue', value: fmt(metrics.queueDepth) });
	}

	if (metrics?.arrivals !== undefined && metrics.arrivals !== null) {
		result.push({ label: 'Arrivals', value: fmt(metrics.arrivals) });
	}

	if (metrics?.served !== undefined && metrics.served !== null) {
		result.push({ label: 'Served', value: fmt(metrics.served) });
	}

	if (metrics?.errors !== undefined && metrics.errors !== null) {
		const errVal = Number(metrics.errors);
		if (errVal > 0) {
			result.push({ label: 'Errors', value: fmt(metrics.errors) });
		}
	}

	if (derived?.capacity !== undefined && derived.capacity !== null) {
		result.push({ label: 'Capacity', value: fmt(derived.capacity) });
	}

	return result;
}

/**
 * Extract a single metric series from state window data for sparkline display.
 * Returns an array of values (one per bin) for the given node and metric.
 */
export function extractSparklineValues(
	stateNodes: Record<string, unknown>[],
	nodeId: string,
	metricPath: 'derived.utilization' | 'metrics.queueDepth' | 'metrics.arrivals' | 'metrics.served'
): number[] {
	const node = stateNodes.find((n) => (n.id as string) === nodeId);
	if (!node) return [];

	const [group, field] = metricPath.split('.');
	const groupObj = node[group] as Record<string, unknown> | undefined;
	if (!groupObj) return [];

	const val = groupObj[field];
	if (typeof val === 'number') return [val];
	return [];
}

/**
 * Find the node with highest utilization, for auto-pinning.
 */
export function findHighestUtilizationNode(
	stateNodes: Record<string, unknown>[]
): { id: string; kind?: string } | null {
	let bestId: string | null = null;
	let bestKind: string | undefined;
	let bestUtil = -1;

	for (const node of stateNodes) {
		const id = node.id as string;
		if (!id) continue;
		const derived = node.derived as Record<string, unknown> | undefined;
		const util = Number(derived?.utilization);
		if (isFinite(util) && util > bestUtil) {
			bestUtil = util;
			bestId = id;
			bestKind = node.kind as string | undefined;
		}
	}

	return bestId ? { id: bestId, kind: bestKind } : null;
}
