import { describe, it, expect } from 'vitest';
import {
	buildHeatmapGrid,
	buildBinAxisLabels,
	type HeatmapGridInput,
} from './heatmap-geometry.js';
import type { MetricDef } from '$lib/utils/metric-defs.js';

/**
 * Heatmap grid geometry — pure helpers that take the `state_window` response shape,
 * a metric def, sort/filter options, and produce the row + cell data structure the
 * heatmap Svelte view iterates to render. Covers:
 *   - Row state classification (has-data vs metric-undefined-for-node).
 *   - Cell state classification (observed vs no-data-for-bin).
 *   - Series extraction via metric-defs (seriesKey + byClass handling).
 *   - Row filtering by class filter + row-stability toggle.
 *   - Row sorting via the heatmap-sort helper (delegated; this test suite covers
 *     the wiring, not the sort algorithm internals).
 *   - Bucket assignment (for the data-value-bucket Playwright seam) via
 *     shared-color-domain's bucketFromDomain helper.
 *
 * Kept as a pure helper so the view component stays thin and rendering can be
 * verified via Playwright without re-deriving the expected geometry.
 */

const UTIL: MetricDef = {
	id: 'utilization',
	label: 'Utilization',
	path: 'derived.utilization',
	seriesKey: 'utilization',
	format: (v) => `${Math.round(v * 100)}%`,
};

const QUEUE: MetricDef = {
	id: 'queue',
	label: 'Queue',
	path: 'metrics.queue',
	seriesKey: 'queue',
	format: (v) => v.toFixed(0),
};

// Window nodes in the wire response are permissive `Record<string, unknown>`;
// the tests use this type alias where they emit ill-shaped fixtures to exercise
// defensive branches (non-array series, missing id, etc.).
type HeatmapWindowNodeMaybe = HeatmapGridInput['windowNodes'];

function windowNode(id: string, series: Record<string, ReadonlyArray<number | null>>) {
	return { id, series };
}

function windowNodeByClass(
	id: string,
	byClass: Record<string, Record<string, ReadonlyArray<number | null>>>
) {
	return { id, byClass };
}

const BASE: Omit<HeatmapGridInput, 'windowNodes' | 'metric' | 'binCount'> = {
	graphEdges: [],
	activeClasses: new Set(),
	sortMode: 'id',
	rowStabilityOn: false,
};

describe('buildHeatmapGrid — cells', () => {
	it('renders one row per node with one cell per bin', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('a', { utilization: [0.1, 0.2, 0.3] }),
				windowNode('b', { utilization: [0.4, 0.5, 0.6] }),
			],
			metric: UTIL,
			binCount: 3,
		});
		expect(grid.visibleRows.length).toBe(2);
		for (const row of grid.visibleRows) {
			expect(row.cells.length).toBe(3);
		}
	});

	it('classifies each cell by value', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [windowNode('a', { utilization: [0.2, null, 0] })],
			metric: UTIL,
			binCount: 3,
		});
		const [row] = grid.visibleRows;
		expect(row.rowState).toBe('has-data');
		expect(row.cells[0].state).toBe('observed');
		expect(row.cells[0].value).toBe(0.2);
		expect(row.cells[1].state).toBe('no-data-for-bin');
		expect(row.cells[2].state).toBe('observed');
		expect(row.cells[2].value).toBe(0);
	});

	it('marks a node as metric-undefined when the metric series is absent', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('a', { utilization: [0.1, 0.2] }),
				windowNode('b', { queue: [3, 4] }), // no utilization series
			],
			metric: UTIL,
			binCount: 2,
		});
		const rows = grid.visibleRows.map((r) => ({ id: r.id, state: r.rowState }));
		expect(rows).toEqual([
			{ id: 'a', state: 'has-data' },
			{ id: 'b', state: 'metric-undefined-for-node' },
		]);
	});

	it('metric-undefined row still carries placeholder cells of length binCount', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [windowNode('a', { queue: [3, 4, 5] })],
			metric: UTIL,
			binCount: 3,
		});
		const [row] = grid.visibleRows;
		expect(row.rowState).toBe('metric-undefined-for-node');
		expect(row.cells.length).toBe(3);
		for (const cell of row.cells) {
			// Row-level muted — placeholder cells are not observed and not per-cell
			// hatched; the component renders the row uniformly muted.
			expect(cell.state).toBe('no-data-for-bin');
			expect(cell.value).toBeNull();
		}
	});
});

describe('buildHeatmapGrid — class filter + row stability', () => {
	it('hides filtered rows by default', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', { vip: { utilization: [0.1, 0.2] } }),
				windowNodeByClass('b', { std: { utilization: [0.3, 0.4] } }),
			],
			metric: UTIL,
			binCount: 2,
			activeClasses: new Set(['vip']),
		});
		// b has only std class — its selected-class series is undefined, so row is
		// metric-undefined. With row-stability off it gets hidden.
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['a']);
	});

	it('with row-stability on, filtered rows sink to the bottom as dimmed', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', { vip: { utilization: [0.1, 0.2] } }),
				windowNodeByClass('b', { std: { utilization: [0.3, 0.4] } }),
			],
			metric: UTIL,
			binCount: 2,
			activeClasses: new Set(['vip']),
			rowStabilityOn: true,
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['a', 'b']);
		expect(grid.visibleRows[0].filtered).toBe(false);
		expect(grid.visibleRows[1].filtered).toBe(true);
	});

	it('filtered rows with row-stability on have no observable cells (their values are excluded from the domain)', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', { vip: { utilization: [0.5] } }),
				windowNodeByClass('b', { std: { utilization: [0.9] } }),
			],
			metric: UTIL,
			binCount: 1,
			activeClasses: new Set(['vip']),
			rowStabilityOn: true,
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['a', 'b']);
		const domain = grid.domain;
		expect(domain).not.toBeNull();
		// domain should be [0.5, 0.5] — b's 0.9 is filtered out.
		expect(domain![0]).toBe(0.5);
		expect(domain![1]).toBe(0.5);
	});

	it('empty class filter match → empty visibleRows', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', { vip: { utilization: [0.1] } }),
				windowNodeByClass('b', { std: { utilization: [0.3] } }),
			],
			metric: UTIL,
			binCount: 1,
			activeClasses: new Set(['enterprise']),
		});
		expect(grid.visibleRows.length).toBe(0);
		expect(grid.isEmptyAfterFilter).toBe(true);
	});

	it('no class filter → all rows visible, none filtered', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('a', { utilization: [0.1] }),
				windowNode('b', { utilization: [0.3] }),
			],
			metric: UTIL,
			binCount: 1,
		});
		expect(grid.visibleRows.length).toBe(2);
		expect(grid.visibleRows.every((r) => !r.filtered)).toBe(true);
		expect(grid.isEmptyAfterFilter).toBe(false);
	});
});

describe('buildHeatmapGrid — sort (pin-agnostic, pinned rows keep natural position)', () => {
	it('sorts rows by id (alphabetical) when sortMode=id', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('c', { utilization: [0.1] }),
				windowNode('a', { utilization: [0.1] }),
				windowNode('b', { utilization: [0.1] }),
			],
			metric: UTIL,
			binCount: 1,
			sortMode: 'id',
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});

	it('pinned rows keep their natural sort position — no float to the top (mode=id)', () => {
		// Pin 'c', which under pure id sort should land at position 2. It must STAY
		// at position 2 — sort is pin-agnostic now. The pin glyph (AC10) is the sole
		// pinned-row indicator.
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('a', { utilization: [0.9] }),
				windowNode('b', { utilization: [0.1] }),
				windowNode('c', { utilization: [0.5] }),
			],
			metric: UTIL,
			binCount: 1,
			sortMode: 'id',
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});

	it('pinned row stays in its natural middle position under topological sort', () => {
		// a → b → c. Pin the middle node 'b'. Under the old contract 'b' floated to
		// position 0; under the new contract it stays at position 1 (its natural
		// topological slot).
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('c', { utilization: [0.1] }),
				windowNode('a', { utilization: [0.2] }),
				windowNode('b', { utilization: [0.3] }),
			],
			metric: UTIL,
			binCount: 1,
			sortMode: 'topological',
			graphEdges: [
				{ from: 'a', to: 'b' },
				{ from: 'b', to: 'c' },
			],
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});

	it('topological mode uses graph edges for ordering', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('z', { utilization: [0.1] }),
				windowNode('a', { utilization: [0.2] }),
				windowNode('m', { utilization: [0.3] }),
			],
			metric: UTIL,
			binCount: 1,
			sortMode: 'topological',
			graphEdges: [
				{ from: 'a', to: 'm' },
				{ from: 'm', to: 'z' },
			],
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['a', 'm', 'z']);
	});
});

describe('buildHeatmapGrid — domain + buckets', () => {
	it('computes domain over the visible, observed cells', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('a', { utilization: [0.1, 0.2, 0.3] }),
				windowNode('b', { utilization: [0.4, 0.5, 0.6] }),
			],
			metric: UTIL,
			binCount: 3,
		});
		expect(grid.domain).not.toBeNull();
		const [lo, hi] = grid.domain!;
		expect(lo).toBe(0.1);
		expect(hi).toBeLessThanOrEqual(0.6);
		expect(hi).toBeGreaterThan(0.3);
	});

	it('assigns low / mid / high buckets to observed cells', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('a', { utilization: [0, 0.5, 1] }),
			],
			metric: UTIL,
			binCount: 3,
		});
		const [row] = grid.visibleRows;
		expect(row.cells[0].bucket).toBe('low');
		expect(row.cells[1].bucket).toBe('mid');
		expect(row.cells[2].bucket).toBe('high');
	});

	it('no-data cells get bucket no-data', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [windowNode('a', { utilization: [0.2, null, 0.6] })],
			metric: UTIL,
			binCount: 3,
		});
		const [row] = grid.visibleRows;
		expect(row.cells[1].bucket).toBe('no-data');
	});

	it('empty grid → domain is null', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [],
			metric: UTIL,
			binCount: 3,
		});
		expect(grid.domain).toBeNull();
	});

	it('grid with only no-data cells → domain is null', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [windowNode('a', { utilization: [null, null] })],
			metric: UTIL,
			binCount: 2,
		});
		expect(grid.domain).toBeNull();
		const [row] = grid.visibleRows;
		// Row is metric-undefined because no finite bins exist.
		expect(row.rowState).toBe('metric-undefined-for-node');
	});
});

describe('buildHeatmapGrid — normalized value for coloring', () => {
	it('observed cells carry a normalized value in [0, 1]', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [windowNode('a', { utilization: [0, 0.5, 1] })],
			metric: UTIL,
			binCount: 3,
		});
		const [row] = grid.visibleRows;
		for (const c of row.cells) {
			if (c.state === 'observed') {
				expect(c.normalized).not.toBeNull();
				expect(c.normalized!).toBeGreaterThanOrEqual(0);
				expect(c.normalized!).toBeLessThanOrEqual(1);
			}
		}
	});

	it('no-data cells have null normalized', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [windowNode('a', { utilization: [0.2, null] })],
			metric: UTIL,
			binCount: 2,
		});
		const [row] = grid.visibleRows;
		expect(row.cells[1].normalized).toBeNull();
	});
});

describe('buildHeatmapGrid — class filter with multi-class byClass nodes', () => {
	it('sums selected-class series per bin', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', {
					vip: { queue: [1, 2, 3] },
					std: { queue: [10, 20, 30] },
				}),
			],
			metric: QUEUE,
			binCount: 3,
			activeClasses: new Set(['vip', 'std']),
		});
		const [row] = grid.visibleRows;
		// When both classes active, per-bin sum.
		expect(row.cells[0].value).toBe(11);
		expect(row.cells[1].value).toBe(22);
		expect(row.cells[2].value).toBe(33);
	});
});

describe('buildHeatmapGrid — edge cases for branch coverage', () => {
	it('skips nodes without an id', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				{ series: { utilization: [0.1] } }, // no id — skipped
				windowNode('b', { utilization: [0.3] }),
			] as HeatmapWindowNodeMaybe,
			metric: UTIL,
			binCount: 1,
		});
		expect(grid.visibleRows.length).toBe(1);
		expect(grid.visibleRows[0].id).toBe('b');
		expect(grid.totalRows).toBe(1);
	});

	it('nodes with neither series nor byClass → metric-undefined', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [{ id: 'empty' }],
			metric: UTIL,
			binCount: 2,
		});
		const [row] = grid.visibleRows;
		expect(row.rowState).toBe('metric-undefined-for-node');
	});

	it('class filter active + node has only flat series (no byClass) → filtered out', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [windowNode('a', { utilization: [0.5] })],
			metric: UTIL,
			binCount: 1,
			activeClasses: new Set(['vip']),
		});
		// With class filter active and no byClass data, the row is class-filtered
		// out; with row-stability off the row disappears entirely.
		expect(grid.visibleRows.length).toBe(0);
	});

	it('class filter active + row-stability on + node has only flat series → appears as filtered', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [windowNode('a', { utilization: [0.5] })],
			metric: UTIL,
			binCount: 1,
			activeClasses: new Set(['vip']),
			rowStabilityOn: true,
		});
		expect(grid.visibleRows.length).toBe(1);
		expect(grid.visibleRows[0].filtered).toBe(true);
	});

	it('nodes whose series value is not an array → metric-undefined', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [{ id: 'a', series: { utilization: 'bad' } }] as HeatmapWindowNodeMaybe,
			metric: UTIL,
			binCount: 2,
		});
		const [row] = grid.visibleRows;
		expect(row.rowState).toBe('metric-undefined-for-node');
	});

	it('accepts a clipPercentile option and uses it for domain computation', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('a', { utilization: [0.1, 0.2, 0.3, 1000] }),
			],
			metric: UTIL,
			binCount: 4,
			clipPercentile: 50,
		});
		expect(grid.domain).not.toBeNull();
		// 50p on [0.1, 0.2, 0.3, 1000] is 0.25; max is therefore ~0.25, far from 1000.
		expect(grid.domain![1]).toBeLessThan(1);
	});

	it('uses a pre-computed topoOrder when supplied (skips Kahn)', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('a', { utilization: [0.1] }),
				windowNode('b', { utilization: [0.2] }),
			],
			metric: UTIL,
			binCount: 1,
			sortMode: 'topological',
			// Deliberately provide edges that would put b first — the topoOrder wins.
			graphEdges: [{ from: 'b', to: 'a' }],
			topoOrder: ['a', 'b'],
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['a', 'b']);
	});

	it('filtered rows respect the active sort order within the dimmed block', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', { vip: { utilization: [0.1] } }),
				windowNodeByClass('z', { std: { utilization: [0.2] } }),
				windowNodeByClass('m', { std: { utilization: [0.3] } }),
			],
			metric: UTIL,
			binCount: 1,
			activeClasses: new Set(['vip']),
			rowStabilityOn: true,
			sortMode: 'id',
		});
		const ids = grid.visibleRows.map((r) => r.id);
		// 'a' is non-filtered (vip data). 'm' and 'z' are filtered, sorted by id.
		expect(ids).toEqual(['a', 'm', 'z']);
		expect(grid.visibleRows[0].filtered).toBe(false);
		expect(grid.visibleRows[1].filtered).toBe(true);
		expect(grid.visibleRows[2].filtered).toBe(true);
	});

	it('class-filter + row-stability off: out-of-filter rows are omitted entirely', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', { vip: { utilization: [0.1] } }),
				windowNodeByClass('b', { std: { utilization: [0.2] } }),
			],
			metric: UTIL,
			binCount: 1,
			activeClasses: new Set(['vip']),
			rowStabilityOn: false,
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['a']);
	});

	it('sumClassSeries handles non-finite values in the first class with finite override from later class', () => {
		// First class contributes [NaN], second contributes [0.5]. Sum should be 0.5.
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', {
					express: { utilization: [null, null] },
					standard: { utilization: [0.5, 0.6] },
				}),
			],
			metric: UTIL,
			binCount: 2,
			activeClasses: new Set(['express', 'standard']),
		});
		const [row] = grid.visibleRows;
		expect(row.cells[0].value).toBe(0.5);
		expect(row.cells[1].value).toBe(0.6);
	});

	it('sumClassSeries leaves first-class value unchanged when later class supplies non-finite', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', {
					express: { utilization: [0.3] },
					standard: { utilization: [null] },
				}),
			],
			metric: UTIL,
			binCount: 1,
			activeClasses: new Set(['express', 'standard']),
		});
		const [row] = grid.visibleRows;
		expect(row.cells[0].value).toBe(0.3);
	});

	it('class-mode with a class absent from the node → skipped silently', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', { vip: { utilization: [0.5] } }),
			],
			metric: UTIL,
			binCount: 1,
			activeClasses: new Set(['vip', 'missing']),
		});
		expect(grid.visibleRows[0].cells[0].value).toBe(0.5);
	});

	it('class-mode where class data has a non-array series → skipped', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', {
					vip: { utilization: 'bad' },
					std: { utilization: [0.2] },
				} as unknown as Record<string, Record<string, ReadonlyArray<number | null>>>),
			],
			metric: UTIL,
			binCount: 1,
			activeClasses: new Set(['vip', 'std']),
		});
		expect(grid.visibleRows[0].cells[0].value).toBe(0.2);
	});

	it('node with no series but byClass only (no class filter) → sums all classes', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNodeByClass('a', {
					vip: { utilization: [0.1, 0.2] },
					std: { utilization: [0.3, 0.4] },
				}),
			],
			metric: UTIL,
			binCount: 2,
			activeClasses: new Set(),
		});
		const [row] = grid.visibleRows;
		expect(row.cells[0].value).toBeCloseTo(0.4, 5);
		expect(row.cells[1].value).toBeCloseTo(0.6, 5);
	});

	it('variance sort orders rows by spread descending', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('low-var', { utilization: [0.5, 0.5, 0.5] }),
				windowNode('high-var', { utilization: [0, 0.5, 1] }),
			],
			metric: UTIL,
			binCount: 3,
			sortMode: 'variance',
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['high-var', 'low-var']);
	});

	it('mean sort orders rows by mean descending', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('low', { utilization: [0.1, 0.2] }),
				windowNode('high', { utilization: [0.8, 0.9] }),
			],
			metric: UTIL,
			binCount: 2,
			sortMode: 'mean',
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['high', 'low']);
	});

	it('max sort orders rows by per-row max descending', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('a', { utilization: [0.9, 0.1] }),
				windowNode('b', { utilization: [0.8, 0.2] }),
			],
			metric: UTIL,
			binCount: 2,
			sortMode: 'max',
		});
		expect(grid.visibleRows.map((r) => r.id)).toEqual(['a', 'b']);
	});

	it('metric-undefined cells get bucket no-data and normalized null', () => {
		const grid = buildHeatmapGrid({
			...BASE,
			windowNodes: [
				windowNode('a', { utilization: [0.5] }),
				windowNode('b', { queue: [10] }), // no utilization — row-level muted
			],
			metric: UTIL,
			binCount: 1,
		});
		const muted = grid.visibleRows.find((r) => r.id === 'b')!;
		expect(muted.rowState).toBe('metric-undefined-for-node');
		expect(muted.cells[0].bucket).toBe('no-data');
		expect(muted.cells[0].normalized).toBeNull();
	});
});

describe('buildBinAxisLabels', () => {
	it('emits one entry per strided bin with bin index and the provided formatter', () => {
		const labels = buildBinAxisLabels({
			binCount: 6,
			stride: 2,
			formatBinLabel: (bin) => `B${bin}`,
		});
		expect(labels.map((l) => l.bin)).toEqual([0, 2, 4]);
		expect(labels.map((l) => l.label)).toEqual(['B0', 'B2', 'B4']);
	});

	it('degenerate stride 1 → one label per bin', () => {
		const labels = buildBinAxisLabels({
			binCount: 3,
			stride: 1,
			formatBinLabel: (bin) => `${bin}`,
		});
		expect(labels.length).toBe(3);
	});

	it('empty binCount → empty label list', () => {
		const labels = buildBinAxisLabels({
			binCount: 0,
			stride: 1,
			formatBinLabel: (bin) => `${bin}`,
		});
		expect(labels).toEqual([]);
	});

	it('stride larger than binCount → single label at bin 0', () => {
		const labels = buildBinAxisLabels({
			binCount: 3,
			stride: 10,
			formatBinLabel: (bin) => `${bin}`,
		});
		expect(labels.length).toBe(1);
		expect(labels[0].bin).toBe(0);
	});
});
