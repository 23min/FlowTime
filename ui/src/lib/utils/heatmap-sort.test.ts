import { describe, it, expect } from 'vitest';
import {
	sortHeatmapRows,
	topologicalOrder,
	type SortMode,
	type HeatmapRowInput,
} from './heatmap-sort.js';

/**
 * Heatmap row-sort comparators — m-E21-06 AC6.
 *
 * Sort modes: 'topological' (default) | 'id' | 'max' | 'mean' | 'variance'.
 * Pinned rows sort in their natural position within the active mode — they are NOT
 * floated to the top. The pin glyph in the row-label gutter (AC10) is the sole
 * pinned-row indicator. Tie-break is node-id (alphabetical, locale-insensitive) to
 * keep the output deterministic.
 *
 * `sortHeatmapRows` returns a new array; it does not mutate the input.
 */

function row(id: string, series: (number | null | undefined)[]): HeatmapRowInput {
	return { id, series };
}

describe('topologicalOrder — Kahn algorithm with node-id tiebreak', () => {
	it('chain: a → b → c → d', () => {
		const order = topologicalOrder(
			['a', 'b', 'c', 'd'],
			[
				{ from: 'a', to: 'b' },
				{ from: 'b', to: 'c' },
				{ from: 'c', to: 'd' },
			]
		);
		expect(order).toEqual(['a', 'b', 'c', 'd']);
	});

	it('fan-out with sibling tiebreak by id', () => {
		// a branches to c, b (in that insertion order); expect a, b, c by id tiebreak.
		const order = topologicalOrder(
			['a', 'c', 'b'],
			[
				{ from: 'a', to: 'c' },
				{ from: 'a', to: 'b' },
			]
		);
		expect(order).toEqual(['a', 'b', 'c']);
	});

	it('disconnected components: pure alpha order', () => {
		const order = topologicalOrder(['z', 'm', 'a'], []);
		expect(order).toEqual(['a', 'm', 'z']);
	});

	it('diamond: a → {b, c} → d', () => {
		const order = topologicalOrder(
			['a', 'b', 'c', 'd'],
			[
				{ from: 'a', to: 'b' },
				{ from: 'a', to: 'c' },
				{ from: 'b', to: 'd' },
				{ from: 'c', to: 'd' },
			]
		);
		expect(order[0]).toBe('a');
		expect(order[3]).toBe('d');
		expect(new Set(order.slice(1, 3))).toEqual(new Set(['b', 'c']));
		// tiebreak by id → b before c
		expect(order).toEqual(['a', 'b', 'c', 'd']);
	});

	it('empty input → empty output', () => {
		expect(topologicalOrder([], [])).toEqual([]);
	});

	it('single node → single-element output', () => {
		expect(topologicalOrder(['only'], [])).toEqual(['only']);
	});

	it('edge referencing unknown node is ignored', () => {
		// 'x' has no dependency among the requested nodes.
		const order = topologicalOrder(['a', 'b'], [{ from: 'x', to: 'a' }]);
		expect(order).toEqual(['a', 'b']);
	});

	it('cycle: fall back to id order for the cyclic residue', () => {
		// a ↔ b — neither can be emitted via Kahn. Post-cycle fallback emits remaining
		// nodes in alphabetical order so the caller still gets a usable ordering.
		const order = topologicalOrder(
			['b', 'a'],
			[
				{ from: 'a', to: 'b' },
				{ from: 'b', to: 'a' },
			]
		);
		expect(order).toEqual(['a', 'b']);
	});
});

describe('sortHeatmapRows — empty / single', () => {
	it('empty input → empty output', () => {
		expect(sortHeatmapRows([], { mode: 'id' })).toEqual([]);
	});

	it('single-node input → single-element output', () => {
		const rows = [row('only', [1, 2, 3])];
		expect(sortHeatmapRows(rows, { mode: 'id' }).map((r) => r.id)).toEqual(['only']);
	});
});

describe('sortHeatmapRows — mode: id (alphabetical)', () => {
	it('sorts ids ascending', () => {
		const rows = [row('c', []), row('a', []), row('b', [])];
		const out = sortHeatmapRows(rows, { mode: 'id' });
		expect(out.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});

	it('does not mutate the input array', () => {
		const rows = [row('c', []), row('a', [])];
		const original = rows.map((r) => r.id);
		sortHeatmapRows(rows, { mode: 'id' });
		expect(rows.map((r) => r.id)).toEqual(original);
	});
});

describe('sortHeatmapRows — mode: max (descending) with tie-break by id', () => {
	it('orders by max value descending', () => {
		const rows = [row('lo', [0.1, 0.2]), row('hi', [0.9, 0.5]), row('mid', [0.4, 0.3])];
		const out = sortHeatmapRows(rows, { mode: 'max' });
		expect(out.map((r) => r.id)).toEqual(['hi', 'mid', 'lo']);
	});

	it('all-equal max values fall back to id order (deterministic)', () => {
		const rows = [row('c', [0.5]), row('a', [0.5]), row('b', [0.5])];
		const out = sortHeatmapRows(rows, { mode: 'max' });
		expect(out.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});

	it('ignores non-finite values when computing max', () => {
		const rows = [row('x', [NaN, null, 0.1]), row('y', [0.2, Infinity, null])];
		// x max = 0.1, y max = 0.2 → y, x.
		const out = sortHeatmapRows(rows, { mode: 'max' });
		expect(out.map((r) => r.id)).toEqual(['y', 'x']);
	});

	it('row with no finite values sinks to the bottom, id-sorted among themselves', () => {
		const rows = [row('y', [null, NaN]), row('live', [0.1]), row('x', [undefined])];
		const out = sortHeatmapRows(rows, { mode: 'max' });
		expect(out.map((r) => r.id)).toEqual(['live', 'x', 'y']);
	});
});

describe('sortHeatmapRows — mode: mean (descending) with tie-break by id', () => {
	it('orders by mean value descending, ignoring missing cells', () => {
		const rows = [
			row('high', [0.9, 0.9, 0.9]), // mean 0.9
			row('mid', [0.5, 0.5, null]), // mean 0.5
			row('low', [0.1, null, null]), // mean 0.1
		];
		const out = sortHeatmapRows(rows, { mode: 'mean' });
		expect(out.map((r) => r.id)).toEqual(['high', 'mid', 'low']);
	});

	it('all-equal means fall back to id', () => {
		const rows = [row('c', [0.5, 0.5]), row('a', [0.5, 0.5]), row('b', [0.5, 0.5])];
		const out = sortHeatmapRows(rows, { mode: 'mean' });
		expect(out.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});

	it('row with zero finite samples sinks, id-sorted', () => {
		const rows = [row('y', []), row('x', []), row('data', [0.1])];
		const out = sortHeatmapRows(rows, { mode: 'mean' });
		expect(out.map((r) => r.id)).toEqual(['data', 'x', 'y']);
	});
});

describe('sortHeatmapRows — mode: variance (descending) with tie-break by id', () => {
	it('orders by variance descending', () => {
		const rows = [
			row('flat', [0.5, 0.5, 0.5]), // var 0
			row('spiky', [0, 1, 0, 1]), // var 0.25
			row('noisy', [0.2, 0.3, 0.5, 0.9]), // ~0.08
		];
		const out = sortHeatmapRows(rows, { mode: 'variance' });
		expect(out.map((r) => r.id)).toEqual(['spiky', 'noisy', 'flat']);
	});

	it('series with fewer than 2 finite samples → variance = 0 (sinks by tiebreak)', () => {
		const rows = [row('one', [0.5]), row('none', []), row('two', [0.1, 0.9])];
		const out = sortHeatmapRows(rows, { mode: 'variance' });
		// two is only row with variance > 0.
		expect(out[0].id).toBe('two');
	});

	it('all-equal variance rows fall back to id', () => {
		const rows = [row('c', [0.5, 0.5]), row('a', [0.5, 0.5]), row('b', [0.5, 0.5])];
		const out = sortHeatmapRows(rows, { mode: 'variance' });
		expect(out.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});
});

describe('sortHeatmapRows — mode: topological', () => {
	it('uses topologicalOrder given edges and tiebreaks by id', () => {
		const rows = [row('c', []), row('a', []), row('b', [])];
		const out = sortHeatmapRows(rows, {
			mode: 'topological',
			edges: [
				{ from: 'a', to: 'b' },
				{ from: 'b', to: 'c' },
			],
		});
		expect(out.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});

	it('falls back to id order when no edges supplied (empty graph)', () => {
		const rows = [row('c', []), row('a', []), row('b', [])];
		const out = sortHeatmapRows(rows, { mode: 'topological' });
		expect(out.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});

	it('pre-computed order is respected when supplied via topoOrder', () => {
		// Parent route may already hold a dag-map topo-sorted list; passing it in avoids
		// re-running Kahn's algorithm on every metric change.
		const rows = [row('b', []), row('a', []), row('c', [])];
		const out = sortHeatmapRows(rows, {
			mode: 'topological',
			topoOrder: ['c', 'a', 'b'],
		});
		expect(out.map((r) => r.id)).toEqual(['c', 'a', 'b']);
	});

	it('ids not present in supplied topoOrder trail at the end, id-sorted', () => {
		const rows = [row('x', []), row('a', []), row('z', []), row('b', [])];
		const out = sortHeatmapRows(rows, {
			mode: 'topological',
			topoOrder: ['a', 'b'],
		});
		expect(out.map((r) => r.id)).toEqual(['a', 'b', 'x', 'z']);
	});
});

describe('sortHeatmapRows — no pinned partitioning (pinned rows sort in their natural position)', () => {
	// Sort must produce exactly the order the active mode dictates, with no pinned-row
	// float. The pin glyph in the row-label gutter (AC10) is the sole pinned-row
	// indicator; position is pin-agnostic.

	it('mode=id with half the rows pinned → pure alphabetical order, NOT pinned-first', () => {
		const rows = [row('d', []), row('a', []), row('c', []), row('b', [])];
		// Pin two rows that would NOT be first under pure id sort.
		const out = sortHeatmapRows(rows, {
			mode: 'id',
			// pinnedIds is no longer part of SortOptions; this call exercises the new
			// contract where the sort is pin-agnostic.
		});
		expect(out.map((r) => r.id)).toEqual(['a', 'b', 'c', 'd']);
	});

	it('mode=topological with a downstream node pinned → pinned node keeps its natural position (after its parent)', () => {
		// a → b → c. Pinning 'c' used to float it to the top under the old contract;
		// under the new contract 'c' stays last (its natural topological position).
		const rows = [row('a', []), row('b', []), row('c', [])];
		const out = sortHeatmapRows(rows, {
			mode: 'topological',
			edges: [
				{ from: 'a', to: 'b' },
				{ from: 'b', to: 'c' },
			],
		});
		expect(out.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});

	it('mode=max with lowest-max row pinned → row sinks to the bottom (no float)', () => {
		// Old contract: pinned 'a' floated to top despite having the lowest max.
		// New contract: 'a' sinks to the bottom under pure max-desc.
		const rows = [row('c', [0.9]), row('a', [0.1]), row('b', [0.5])];
		const out = sortHeatmapRows(rows, { mode: 'max' });
		expect(out.map((r) => r.id)).toEqual(['c', 'b', 'a']);
	});
});

describe('sortHeatmapRows — unknown sort mode falls back to id', () => {
	it('defensive fallback for unexpected mode values', () => {
		const rows = [row('c', []), row('a', []), row('b', [])];
		// Cast forces the branch we need to cover for defensive switch-default.
		const out = sortHeatmapRows(rows, {
			mode: 'unknown' as unknown as SortMode,
		});
		expect(out.map((r) => r.id)).toEqual(['a', 'b', 'c']);
	});
});
