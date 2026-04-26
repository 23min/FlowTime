/**
 * Cell-state classifier for the heatmap view (m-E21-06 AC4).
 *
 * Three logical cell states:
 *   - 'observed'                 — finite numeric value (including 0); colored from the shared domain.
 *   - 'no-data-for-bin'          — per-cell absence (null / undefined / NaN / Infinity); neutral + hatch.
 *   - 'metric-undefined-for-node' — row-level absence; the entire row renders muted (AC4 row-level
 *                                    optimization) instead of per-cell hatch.
 *
 * Keying is per-(node, metric). A `const` node has no `utilization` metric and therefore renders
 * as a row-level-muted row under the utilization metric, but may render normally under a `value`
 * metric defined for it.
 *
 * Consumers call `classifyNodeRowState(series)` once per (node, metric) to decide whether to
 * short-circuit to row-level-muted rendering; if `has-data`, they call `classifyCellState(value)`
 * per bin to distinguish observed from no-data-for-bin.
 */

export type CellState = 'observed' | 'no-data-for-bin';

export type NodeRowState = 'has-data' | 'metric-undefined-for-node';

/**
 * Classify a single cell value. Finite numbers (including 0) are observed;
 * everything else — undefined / null / NaN / ±Infinity — is no-data-for-bin.
 */
export function classifyCellState(value: number | null | undefined): CellState {
	if (value === null || value === undefined) return 'no-data-for-bin';
	if (!Number.isFinite(value)) return 'no-data-for-bin';
	return 'observed';
}

/**
 * Classify a (node, metric) row. Returns 'metric-undefined-for-node' when the series is
 * absent or contains no finite values; returns 'has-data' when at least one bin carries a
 * finite value. The caller decides what to render per cell inside a 'has-data' row via
 * `classifyCellState`.
 *
 * An all-Infinity series is treated as row-level-muted — infinities are not legitimate
 * observed values for any supported metric.
 */
export function classifyNodeRowState(
	series: ReadonlyArray<number | null | undefined> | undefined
): NodeRowState {
	if (series === undefined) return 'metric-undefined-for-node';
	if (series.length === 0) return 'metric-undefined-for-node';
	for (const v of series) {
		if (v === null || v === undefined) continue;
		if (Number.isFinite(v)) return 'has-data';
	}
	return 'metric-undefined-for-node';
}
