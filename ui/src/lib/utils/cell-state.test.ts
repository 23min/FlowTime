import { describe, it, expect } from 'vitest';
import {
	classifyCellState,
	classifyNodeRowState,
	type CellState,
	type NodeRowState,
} from './cell-state.js';

/**
 * Cell-state classifier — m-E21-06 AC4.
 *
 * Three states:
 *   - observed              (colored, includes 0)
 *   - no-data-for-bin       (per-cell, neutral + hatch)
 *   - metric-undefined-for-node (row-level, muted row)
 *
 * Keying is per-(node, metric). A node can be row-level-muted under one
 * metric and observed under another.
 *
 * Row-level optimization: if the series for this (node, metric) is undefined
 * OR the series has no defined values across *any* bin, the row is muted
 * once (not per-cell).
 */

describe('classifyCellState — per-cell decisions', () => {
	it('finite number is observed', () => {
		expect(classifyCellState(0.42)).toBe<CellState>('observed');
	});

	it('zero is observed (not no-data)', () => {
		expect(classifyCellState(0)).toBe<CellState>('observed');
	});

	it('negative finite is observed', () => {
		expect(classifyCellState(-1.5)).toBe<CellState>('observed');
	});

	it('undefined is no-data-for-bin', () => {
		expect(classifyCellState(undefined)).toBe<CellState>('no-data-for-bin');
	});

	it('null is no-data-for-bin', () => {
		expect(classifyCellState(null)).toBe<CellState>('no-data-for-bin');
	});

	it('NaN is no-data-for-bin', () => {
		expect(classifyCellState(NaN)).toBe<CellState>('no-data-for-bin');
	});

	it('Infinity is no-data-for-bin', () => {
		expect(classifyCellState(Infinity)).toBe<CellState>('no-data-for-bin');
	});

	it('-Infinity is no-data-for-bin', () => {
		expect(classifyCellState(-Infinity)).toBe<CellState>('no-data-for-bin');
	});
});

describe('classifyNodeRowState — row-level decisions', () => {
	it('undefined series → metric-undefined-for-node (row-level muted)', () => {
		expect(classifyNodeRowState(undefined)).toBe<NodeRowState>('metric-undefined-for-node');
	});

	it('empty array → metric-undefined-for-node (row-level muted)', () => {
		expect(classifyNodeRowState([])).toBe<NodeRowState>('metric-undefined-for-node');
	});

	it('all-null series → metric-undefined-for-node (row-level muted)', () => {
		expect(classifyNodeRowState([null, null, null])).toBe<NodeRowState>(
			'metric-undefined-for-node'
		);
	});

	it('all-NaN series → metric-undefined-for-node (row-level muted)', () => {
		expect(classifyNodeRowState([NaN, NaN])).toBe<NodeRowState>(
			'metric-undefined-for-node'
		);
	});

	it('mixed null/NaN with no finite → metric-undefined-for-node (row-level muted)', () => {
		expect(classifyNodeRowState([null, NaN, undefined])).toBe<NodeRowState>(
			'metric-undefined-for-node'
		);
	});

	it('at least one finite number → has-data (per-cell classification applies)', () => {
		expect(classifyNodeRowState([null, 0.5, NaN])).toBe<NodeRowState>('has-data');
	});

	it('all finite numbers → has-data', () => {
		expect(classifyNodeRowState([0.1, 0.2, 0.3])).toBe<NodeRowState>('has-data');
	});

	it('single zero counts as has-data (zero is observed)', () => {
		expect(classifyNodeRowState([0])).toBe<NodeRowState>('has-data');
	});

	it('Infinity does not count as finite data — treated as no-data', () => {
		expect(classifyNodeRowState([Infinity, -Infinity])).toBe<NodeRowState>(
			'metric-undefined-for-node'
		);
	});

	it('mix of Infinity and one finite value → has-data', () => {
		expect(classifyNodeRowState([Infinity, 1.0])).toBe<NodeRowState>('has-data');
	});
});
