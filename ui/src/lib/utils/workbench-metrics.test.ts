import { describe, it, expect } from 'vitest';
import { extractNodeMetrics, extractEdgeMetrics, findHighestUtilizationNode } from './workbench-metrics.js';

describe('extractNodeMetrics', () => {
	it('extracts utilization from derived', () => {
		const node = { id: 'a', derived: { utilization: 0.85 }, metrics: {} };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Utilization', value: '85.0%' });
	});

	it('extracts queue from metrics', () => {
		const node = { id: 'a', derived: {}, metrics: { queue: 14.5 } };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Queue', value: '14.5' });
	});

	it('extracts arrivals and served', () => {
		const node = { id: 'a', derived: {}, metrics: { arrivals: 100, served: 95 } };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Arrivals', value: '100.0' });
		expect(result).toContainEqual({ label: 'Served', value: '95.0' });
	});

	it('skips errors when zero', () => {
		const node = { id: 'a', derived: {}, metrics: { errors: 0 } };
		const result = extractNodeMetrics(node);
		expect(result.find((m) => m.label === 'Errors')).toBeUndefined();
	});

	it('includes errors when non-zero', () => {
		const node = { id: 'a', derived: {}, metrics: { errors: 3 } };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Errors', value: '3.00' });
	});

	it('extracts capacity from derived', () => {
		const node = { id: 'a', derived: { capacity: 200 }, metrics: {} };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Capacity', value: '200.0' });
	});

	it('returns empty array for node with no metrics', () => {
		const node = { id: 'a' };
		const result = extractNodeMetrics(node);
		expect(result).toEqual([]);
	});

	it('handles null values gracefully', () => {
		const node = { id: 'a', derived: { utilization: null }, metrics: { arrivals: null } };
		const result = extractNodeMetrics(node);
		expect(result).toEqual([]);
	});

	it('handles NaN values gracefully', () => {
		const node = { id: 'a', derived: { utilization: NaN }, metrics: {} };
		const result = extractNodeMetrics(node);
		expect(result).toEqual([]);
	});

	it('handles Infinity values gracefully', () => {
		const node = { id: 'a', derived: { utilization: Infinity }, metrics: {} };
		const result = extractNodeMetrics(node);
		expect(result).toEqual([]);
	});

	it('formats small values with more precision', () => {
		const node = { id: 'a', derived: {}, metrics: { queue: 0.0042 } };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Queue', value: '0.004' });
	});

	it('formats large values without decimals', () => {
		const node = { id: 'a', derived: {}, metrics: { arrivals: 12345 } };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Arrivals', value: '12345' });
	});

	it('formats values in 1-10 range with two decimals', () => {
		// num branch: >= 1 && < 10
		const node = { id: 'a', derived: {}, metrics: { queue: 2.5 } };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Queue', value: '2.50' });
	});

	it('skips non-finite queue', () => {
		const node = { id: 'a', derived: {}, metrics: { queue: NaN } };
		const result = extractNodeMetrics(node);
		expect(result.find((m) => m.label === 'Queue')).toBeUndefined();
	});

	it('skips non-finite arrivals', () => {
		const node = { id: 'a', derived: {}, metrics: { arrivals: Infinity } };
		const result = extractNodeMetrics(node);
		expect(result.find((m) => m.label === 'Arrivals')).toBeUndefined();
	});

	it('skips non-finite served', () => {
		const node = { id: 'a', derived: {}, metrics: { served: NaN } };
		const result = extractNodeMetrics(node);
		expect(result.find((m) => m.label === 'Served')).toBeUndefined();
	});

	it('skips non-finite errors', () => {
		const node = { id: 'a', derived: {}, metrics: { errors: NaN } };
		const result = extractNodeMetrics(node);
		expect(result.find((m) => m.label === 'Errors')).toBeUndefined();
	});

	it('skips non-finite capacity', () => {
		const node = { id: 'a', derived: { capacity: NaN }, metrics: {} };
		const result = extractNodeMetrics(node);
		expect(result.find((m) => m.label === 'Capacity')).toBeUndefined();
	});

	it('skips negative errors', () => {
		const node = { id: 'a', derived: {}, metrics: { errors: -1 } };
		const result = extractNodeMetrics(node);
		expect(result.find((m) => m.label === 'Errors')).toBeUndefined();
	});
});

describe('extractEdgeMetrics', () => {
	it('extracts flow volume', () => {
		const edge = { from: 'a', to: 'b', flowVolume: 80 };
		const result = extractEdgeMetrics(edge);
		expect(result).toContainEqual({ label: 'Flow', value: '80.0' });
	});

	it('extracts attempt and failure volumes', () => {
		const edge = { attemptVolume: 100, failureVolume: 5 };
		const result = extractEdgeMetrics(edge);
		expect(result).toContainEqual({ label: 'Attempts', value: '100.0' });
		expect(result).toContainEqual({ label: 'Failures', value: '5.00' });
	});

	it('skips zero failure volume', () => {
		const edge = { flowVolume: 80, failureVolume: 0 };
		const result = extractEdgeMetrics(edge);
		expect(result.find((m) => m.label === 'Failures')).toBeUndefined();
	});

	it('returns empty for edge with no metrics', () => {
		expect(extractEdgeMetrics({ from: 'a', to: 'b' })).toEqual([]);
	});

	it('skips non-finite flow volume', () => {
		const edge = { flowVolume: NaN };
		const result = extractEdgeMetrics(edge);
		expect(result.find((m) => m.label === 'Flow')).toBeUndefined();
	});

	it('skips non-finite attempt volume', () => {
		const edge = { attemptVolume: Infinity };
		const result = extractEdgeMetrics(edge);
		expect(result.find((m) => m.label === 'Attempts')).toBeUndefined();
	});

	it('skips non-finite failure volume', () => {
		const edge = { failureVolume: NaN };
		const result = extractEdgeMetrics(edge);
		expect(result.find((m) => m.label === 'Failures')).toBeUndefined();
	});

	it('skips negative failure volume', () => {
		const edge = { flowVolume: 50, failureVolume: -2 };
		const result = extractEdgeMetrics(edge);
		expect(result.find((m) => m.label === 'Failures')).toBeUndefined();
	});

	it('skips null fields', () => {
		const edge = { flowVolume: null, attemptVolume: null, failureVolume: null };
		expect(extractEdgeMetrics(edge)).toEqual([]);
	});
});

describe('findHighestUtilizationNode', () => {
	it('returns node with highest utilization', () => {
		const nodes = [
			{ id: 'a', kind: 'service', derived: { utilization: 0.5 } },
			{ id: 'b', kind: 'queue', derived: { utilization: 0.9 } },
			{ id: 'c', kind: 'service', derived: { utilization: 0.3 } },
		];
		const result = findHighestUtilizationNode(nodes);
		expect(result).toEqual({ id: 'b', kind: 'queue' });
	});

	it('returns null for empty array', () => {
		expect(findHighestUtilizationNode([])).toBeNull();
	});

	it('returns null when no nodes have utilization', () => {
		const nodes = [{ id: 'a', derived: {} }, { id: 'b' }];
		expect(findHighestUtilizationNode(nodes)).toBeNull();
	});

	it('skips nodes with NaN utilization', () => {
		const nodes = [
			{ id: 'a', derived: { utilization: NaN } },
			{ id: 'b', derived: { utilization: 0.7 } },
		];
		const result = findHighestUtilizationNode(nodes);
		expect(result).toEqual({ id: 'b', kind: undefined });
	});

	it('handles single node', () => {
		const nodes = [{ id: 'only', kind: 'service', derived: { utilization: 0.42 } }];
		const result = findHighestUtilizationNode(nodes);
		expect(result).toEqual({ id: 'only', kind: 'service' });
	});
});
