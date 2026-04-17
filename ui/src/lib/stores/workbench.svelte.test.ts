import { describe, it, expect, beforeEach } from 'vitest';
import { workbench } from './workbench.svelte.js';
import { DEFAULT_METRIC, METRIC_DEFS } from '$lib/utils/metric-defs.js';

describe('WorkbenchStore (nodes)', () => {
	beforeEach(() => {
		workbench.clear();
	});

	it('starts empty', () => {
		expect(workbench.pinned).toEqual([]);
		expect(workbench.pinnedEdges).toEqual([]);
		expect(workbench.selectedMetric).toBe(DEFAULT_METRIC);
	});

	it('pin adds a node', () => {
		workbench.pin('a', 'service');
		expect(workbench.pinned).toEqual([{ id: 'a', kind: 'service' }]);
	});

	it('pin is idempotent — duplicate id is ignored', () => {
		workbench.pin('a', 'service');
		workbench.pin('a', 'queue');
		expect(workbench.pinned).toHaveLength(1);
		expect(workbench.pinned[0].kind).toBe('service'); // first kind preserved
	});

	it('pin without kind works', () => {
		workbench.pin('a');
		expect(workbench.pinned[0]).toEqual({ id: 'a', kind: undefined });
	});

	it('unpin removes a specific node', () => {
		workbench.pin('a');
		workbench.pin('b');
		workbench.unpin('a');
		expect(workbench.pinned.map((n) => n.id)).toEqual(['b']);
	});

	it('unpin of non-existent id is a no-op', () => {
		workbench.pin('a');
		workbench.unpin('missing');
		expect(workbench.pinned).toHaveLength(1);
	});

	it('toggle adds when absent', () => {
		workbench.toggle('a', 'service');
		expect(workbench.isPinned('a')).toBe(true);
	});

	it('toggle removes when present', () => {
		workbench.pin('a');
		workbench.toggle('a');
		expect(workbench.isPinned('a')).toBe(false);
	});

	it('selectedIds returns a Set of node ids', () => {
		workbench.pin('a');
		workbench.pin('b');
		const ids = workbench.selectedIds;
		expect(ids).toBeInstanceOf(Set);
		expect(ids.has('a')).toBe(true);
		expect(ids.has('b')).toBe(true);
		expect(ids.size).toBe(2);
	});

	it('isPinned returns false for non-pinned id', () => {
		expect(workbench.isPinned('nope')).toBe(false);
	});
});

describe('WorkbenchStore (edges)', () => {
	beforeEach(() => {
		workbench.clear();
	});

	it('pinEdge adds an edge', () => {
		workbench.pinEdge('a', 'b');
		expect(workbench.pinnedEdges).toEqual([{ from: 'a', to: 'b' }]);
	});

	it('pinEdge is idempotent on the same (from,to) pair', () => {
		workbench.pinEdge('a', 'b');
		workbench.pinEdge('a', 'b');
		expect(workbench.pinnedEdges).toHaveLength(1);
	});

	it('pinEdge distinguishes direction — (a,b) and (b,a) are different', () => {
		workbench.pinEdge('a', 'b');
		workbench.pinEdge('b', 'a');
		expect(workbench.pinnedEdges).toHaveLength(2);
	});

	it('unpinEdge removes the specified pair only', () => {
		workbench.pinEdge('a', 'b');
		workbench.pinEdge('c', 'd');
		workbench.unpinEdge('a', 'b');
		expect(workbench.pinnedEdges).toEqual([{ from: 'c', to: 'd' }]);
	});

	it('unpinEdge of non-existent pair is a no-op', () => {
		workbench.pinEdge('a', 'b');
		workbench.unpinEdge('x', 'y');
		expect(workbench.pinnedEdges).toHaveLength(1);
	});

	it('toggleEdge adds when absent', () => {
		workbench.toggleEdge('a', 'b');
		expect(workbench.pinnedEdges).toHaveLength(1);
	});

	it('toggleEdge removes when present', () => {
		workbench.pinEdge('a', 'b');
		workbench.toggleEdge('a', 'b');
		expect(workbench.pinnedEdges).toHaveLength(0);
	});

	it('selectedEdgeKeys returns keys in "from→to" format', () => {
		workbench.pinEdge('a', 'b');
		workbench.pinEdge('c', 'd');
		const keys = workbench.selectedEdgeKeys;
		expect(keys).toBeInstanceOf(Set);
		expect(keys.has('a\u2192b')).toBe(true);
		expect(keys.has('c\u2192d')).toBe(true);
		expect(keys.size).toBe(2);
	});
});

describe('WorkbenchStore (metric + clear)', () => {
	beforeEach(() => {
		workbench.clear();
	});

	it('selectedMetric can be changed', () => {
		const queueDef = METRIC_DEFS.find((d) => d.id === 'queue')!;
		workbench.selectedMetric = queueDef;
		expect(workbench.selectedMetric).toBe(queueDef);
	});

	it('clear resets nodes, edges, and metric', () => {
		workbench.pin('a');
		workbench.pinEdge('x', 'y');
		const queueDef = METRIC_DEFS.find((d) => d.id === 'queue')!;
		workbench.selectedMetric = queueDef;

		workbench.clear();

		expect(workbench.pinned).toEqual([]);
		expect(workbench.pinnedEdges).toEqual([]);
		expect(workbench.selectedMetric).toBe(DEFAULT_METRIC);
	});
});
