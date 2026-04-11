import { describe, it, expect } from 'vitest';
import { adaptEngineGraph } from './graph-adapter';
import type { EngineGraph } from './engine-session';

describe('adaptEngineGraph', () => {
	it('converts nodes preserving id and kind', () => {
		const eg: EngineGraph = {
			nodes: [
				{ id: 'arrivals', kind: 'const' },
				{ id: 'served', kind: 'expr' },
			],
			edges: [],
		};
		const gr = adaptEngineGraph(eg);
		expect(gr.nodes).toHaveLength(2);
		expect(gr.nodes[0]).toEqual({ id: 'arrivals', kind: 'const' });
		expect(gr.nodes[1]).toEqual({ id: 'served', kind: 'expr' });
	});

	it('synthesizes sequential edge ids', () => {
		const eg: EngineGraph = {
			nodes: [
				{ id: 'a', kind: 'const' },
				{ id: 'b', kind: 'const' },
				{ id: 'c', kind: 'expr' },
			],
			edges: [
				{ from: 'a', to: 'c' },
				{ from: 'b', to: 'c' },
			],
		};
		const gr = adaptEngineGraph(eg);
		expect(gr.edges).toEqual([
			{ id: 'e-0', from: 'a', to: 'c' },
			{ id: 'e-1', from: 'b', to: 'c' },
		]);
	});

	it('handles empty graphs', () => {
		const gr = adaptEngineGraph({ nodes: [], edges: [] });
		expect(gr.nodes).toEqual([]);
		expect(gr.edges).toEqual([]);
		expect(gr.order).toEqual([]);
	});

	it('returns empty order (dag-map-view computes its own topo order)', () => {
		const eg: EngineGraph = {
			nodes: [{ id: 'x', kind: 'const' }],
			edges: [],
		};
		const gr = adaptEngineGraph(eg);
		expect(gr.order).toEqual([]);
	});
});
