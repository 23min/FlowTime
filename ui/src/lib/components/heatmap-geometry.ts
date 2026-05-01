/**
 * Pure geometry / data-shaping helpers for the heatmap view — m-E21-06 AC3/4/5/6/7.
 *
 * Turns a `state_window` response into the row + cell data structure the Svelte
 * heatmap view iterates. Keeps every non-trivial decision (cell classification,
 * series extraction, class-filter handling, row stability, sort, bucket
 * assignment) in testable pure code; the component body only renders.
 */

import type { StateWindowResponse } from '$lib/api/types.js';
import type { MetricDef } from '$lib/utils/metric-defs.js';
import {
	classifyCellState,
	classifyNodeRowState,
	type NodeRowState,
	type CellState,
} from '$lib/utils/cell-state.js';
import {
	computeSharedColorDomain,
	bucketFromDomain,
	type ColorDomainCell,
	type BucketLabel,
} from '$lib/utils/shared-color-domain.js';
import { normalizeValueInDomain } from '$lib/utils/value-normalize.js';
import {
	sortHeatmapRows,
	type SortMode,
	type SortEdge,
	type HeatmapRowInput,
} from '$lib/utils/heatmap-sort.js';

export type HeatmapWindowNode = StateWindowResponse['nodes'][number];

export interface HeatmapGridInput {
	windowNodes: ReadonlyArray<HeatmapWindowNode>;
	metric: MetricDef;
	binCount: number;
	graphEdges: ReadonlyArray<SortEdge>;
	activeClasses: ReadonlySet<string>;
	sortMode: SortMode;
	rowStabilityOn: boolean;
	/** Pre-computed topological order; skips Kahn inside the sort when provided. */
	topoOrder?: ReadonlyArray<string>;
	/** Percentile clip for the shared color domain. Default 99. */
	clipPercentile?: number;
}

export interface HeatmapCell {
	bin: number;
	state: CellState;
	/** Finite numeric value when `state === 'observed'`, otherwise null. */
	value: number | null;
	/** Normalized position in [0, 1] relative to the grid domain, or null when no-data. */
	normalized: number | null;
	/** Bucket label ('low' | 'mid' | 'high' | 'no-data') — the Playwright correctness surface. */
	bucket: BucketLabel;
}

export interface HeatmapRow {
	id: string;
	rowState: NodeRowState;
	/** True when this row is class-filtered and retained via the row-stability toggle. */
	filtered: boolean;
	cells: HeatmapCell[];
}

export interface HeatmapGrid {
	visibleRows: HeatmapRow[];
	/** Shared full-window domain, or null when no observed cells survived filtering. */
	domain: [number, number] | null;
	/** True when `activeClasses` collapsed `visibleRows` to zero. */
	isEmptyAfterFilter: boolean;
	/** Total row count in the response (before any sort / filter). */
	totalRows: number;
}

/**
 * Extract a per-bin series for one node under the active class filter.
 *
 * Matches `buildSparklineSeries` semantics: when `activeClasses` is non-empty,
 * sum per-class series across the selected classes; otherwise read the node-level
 * aggregate series. Returns `undefined` when no series is defined for this node
 * under the current filter (triggers row-level-muted rendering).
 */
function extractSeries(
	node: HeatmapWindowNode,
	seriesKey: string,
	activeClasses: ReadonlySet<string>,
	binCount: number
): ReadonlyArray<number | null> | undefined {
	const n = node as Record<string, unknown>;
	const flatSeries = n.series as Record<string, unknown> | undefined;
	const byClass = n.byClass as Record<string, unknown> | undefined;

	if (activeClasses.size === 0) {
		// Prefer the flat aggregate series when present (matches topology sparkline semantics).
		if (flatSeries) {
			const arr = flatSeries[seriesKey];
			if (Array.isArray(arr)) {
				return (arr as ReadonlyArray<number | null>).slice(0, binCount);
			}
		}
		// Fall back to per-class-sum across ALL classes when the node only exposes byClass data.
		if (byClass) {
			return sumClassSeries(byClass, Object.keys(byClass), seriesKey, binCount);
		}
		return undefined;
	}

	if (!byClass) return undefined;
	return sumClassSeries(byClass, [...activeClasses], seriesKey, binCount);
}

function sumClassSeries(
	byClass: Record<string, unknown>,
	classKeys: ReadonlyArray<string>,
	seriesKey: string,
	binCount: number
): ReadonlyArray<number | null> | undefined {
	let summed: (number | null)[] | undefined;
	for (const cls of classKeys) {
		const classData = byClass[cls] as Record<string, unknown> | undefined;
		if (!classData) continue;
		const arr = classData[seriesKey];
		if (!Array.isArray(arr)) continue;
		const values = arr as ReadonlyArray<number | null>;
		if (!summed) {
			summed = values.slice(0, binCount);
		} else {
			for (let i = 0; i < summed.length && i < values.length; i++) {
				const a = summed[i];
				const b = values[i];
				if (a === null || a === undefined || !Number.isFinite(a)) {
					summed[i] = b;
				} else if (b === null || b === undefined || !Number.isFinite(b)) {
					// leave a
				} else {
					summed[i] = a + b;
				}
			}
		}
	}
	return summed;
}

interface RawRow {
	id: string;
	rowState: NodeRowState;
	filtered: boolean;
	series: ReadonlyArray<number | null>;
}

/**
 * Build the heatmap grid data structure. See HeatmapGridInput for the inputs and
 * HeatmapGrid for the output contract.
 */
export function buildHeatmapGrid(input: HeatmapGridInput): HeatmapGrid {
	const {
		windowNodes,
		metric,
		binCount,
		graphEdges,
		activeClasses,
		sortMode,
		rowStabilityOn,
		topoOrder,
		clipPercentile,
	} = input;

	const hasClassFilter = activeClasses.size > 0;

	// Step 1. For each node, extract the active-class series (or fall back to node-level).
	// Build a "raw row" record that already knows its state + filtered flag.
	const rawRows: RawRow[] = [];
	for (const node of windowNodes) {
		const id = (node as Record<string, unknown>).id as string;
		if (!id) continue;

		// Unfiltered series: always the node-level (or class-summed across ALL classes)
		// to decide whether the metric is defined on this node at all. This lets a node
		// that HAS data but is currently out of the class filter be marked `filtered`
		// rather than `metric-undefined-for-node`.
		const unfilteredSeries = extractSeries(node, metric.seriesKey, new Set(), binCount);
		const filteredSeries = hasClassFilter
			? extractSeries(node, metric.seriesKey, activeClasses, binCount)
			: unfilteredSeries;

		const unfilteredRowState = classifyNodeRowState(unfilteredSeries);

		if (unfilteredRowState === 'metric-undefined-for-node') {
			// The metric is not defined for this node at all. Row is muted under any
			// filter configuration. A placeholder empty series drives binCount cells.
			rawRows.push({
				id,
				rowState: 'metric-undefined-for-node',
				filtered: false,
				series: new Array(binCount).fill(null),
			});
			continue;
		}

		// The metric is defined on this node. Filter membership may still remove it.
		const filteredRowState = classifyNodeRowState(filteredSeries);
		if (hasClassFilter && filteredRowState === 'metric-undefined-for-node') {
			// Class-filtered out. Include only when row-stability is on.
			if (rowStabilityOn) {
				rawRows.push({
					id,
					rowState: 'has-data',
					filtered: true,
					series: new Array(binCount).fill(null),
				});
			}
			continue;
		}

		// At this point `filteredSeries` is guaranteed defined: either (a) no class
		// filter so it equals `unfilteredSeries` which classifyNodeRowState confirmed
		// as has-data, or (b) class filter active with filteredRowState === 'has-data'
		// (class-undefined already early-returned above). The `??` is defensive for
		// the TS compiler.
		rawRows.push({
			id,
			rowState: 'has-data',
			filtered: false,
			series: filteredSeries ?? new Array(binCount).fill(null),
		});
	}

	// Step 2. Compute the shared color domain over the OBSERVED, non-filtered cells.
	const domainCells: ColorDomainCell[] = [];
	for (const row of rawRows) {
		if (row.filtered) continue; // filtered rows are excluded from the domain
		if (row.rowState !== 'has-data') continue;
		for (const v of row.series) {
			const state = classifyCellState(v);
			if (state === 'observed') {
				domainCells.push({ value: v as number, state: 'observed' });
			}
		}
	}
	const domain = computeSharedColorDomain(domainCells, {
		excludeNonObserved: true,
		clipPercentile,
	});

	// Step 3. Assemble cells per row with bucket + normalized position.
	const rowsWithCells: HeatmapRow[] = rawRows.map((raw) => {
		const cells: HeatmapCell[] = [];
		for (let bin = 0; bin < binCount; bin++) {
			const v = raw.series[bin] ?? null;
			let state: CellState;
			if (raw.rowState === 'metric-undefined-for-node' || raw.filtered) {
				state = 'no-data-for-bin';
			} else {
				state = classifyCellState(v);
			}
			const value = state === 'observed' ? (v as number) : null;
			const normalized =
				state === 'observed' && domain !== null
					? normalizeValueInDomain(value, domain)
					: null;
			const bucket: BucketLabel =
				state === 'observed' && domain !== null
					? bucketFromDomain(value, domain)
					: 'no-data';
			cells.push({ bin, state, value, normalized, bucket });
		}
		return {
			id: raw.id,
			rowState: raw.rowState,
			filtered: raw.filtered,
			cells,
		};
	});

	// Step 4. Sort. Filtered rows sink to the bottom; non-filtered rows sort via the
	// active mode. Sort is pin-agnostic (AC10 pin glyph is the sole pinned-row indicator).
	const nonFiltered: HeatmapRow[] = [];
	const filtered: HeatmapRow[] = [];
	for (const row of rowsWithCells) {
		if (row.filtered) filtered.push(row);
		else nonFiltered.push(row);
	}

	const sortInput: HeatmapRowInput[] = nonFiltered.map((r) => ({
		id: r.id,
		series: r.cells.map((c) => (c.state === 'observed' ? c.value : null)),
	}));
	const sorted = sortHeatmapRows(sortInput, {
		mode: sortMode,
		edges: graphEdges,
		topoOrder,
	});
	const byId = new Map(nonFiltered.map((r) => [r.id, r]));
	const sortedNonFiltered = sorted.map((si) => byId.get(si.id)!);

	// Filtered rows respect the active sort (per spec: "sorted independently by the
	// active sort within the dimmed block"). Pin-agnostic, same as non-filtered.
	const filteredSortInput: HeatmapRowInput[] = filtered.map((r) => ({
		id: r.id,
		series: r.cells.map((c) => (c.state === 'observed' ? c.value : null)),
	}));
	const sortedFiltered = sortHeatmapRows(filteredSortInput, {
		mode: sortMode,
		edges: graphEdges,
		topoOrder,
	});
	const filteredById = new Map(filtered.map((r) => [r.id, r]));
	const sortedFilteredRows = sortedFiltered.map((si) => filteredById.get(si.id)!);

	const visibleRows = [...sortedNonFiltered, ...sortedFilteredRows];

	return {
		visibleRows,
		domain,
		isEmptyAfterFilter: visibleRows.length === 0,
		totalRows: rawRows.length,
	};
}

// ---- Bin axis label builder ---------------------------------------------------------

export interface BinAxisLabelsInput {
	binCount: number;
	stride: number;
	formatBinLabel: (bin: number) => string;
}

export interface BinAxisLabel {
	bin: number;
	label: string;
}

/**
 * Build the list of sparse bin-axis labels given a stride (from `pickBinLabelStride`)
 * and a per-bin formatter. Returns one entry per labelled bin; the caller positions
 * them over the column grid.
 */
export function buildBinAxisLabels(input: BinAxisLabelsInput): BinAxisLabel[] {
	const { binCount, stride, formatBinLabel } = input;
	if (binCount <= 0) return [];
	const labels: BinAxisLabel[] = [];
	const effectiveStride = stride <= 0 ? 1 : stride;
	for (let bin = 0; bin < binCount; bin += effectiveStride) {
		labels.push({ bin, label: formatBinLabel(bin) });
	}
	return labels;
}
