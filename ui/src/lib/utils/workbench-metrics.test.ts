import { describe, it, expect } from 'vitest';
import { extractNodeMetrics, findHighestUtilizationNode } from './workbench-metrics.js';

describe('extractNodeMetrics', () => {
	it('extracts utilization from derived', () => {
		const node = { id: 'a', derived: { utilization: 0.85 }, metrics: {} };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Utilization', value: '85.0%' });
	});

	it('extracts queue depth from metrics', () => {
		const node = { id: 'a', derived: {}, metrics: { queueDepth: 14.5 } };
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
		const node = { id: 'a', derived: {}, metrics: { queueDepth: 0.0042 } };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Queue', value: '0.004' });
	});

	it('formats large values without decimals', () => {
		const node = { id: 'a', derived: {}, metrics: { arrivals: 12345 } };
		const result = extractNodeMetrics(node);
		expect(result).toContainEqual({ label: 'Arrivals', value: '12345' });
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
