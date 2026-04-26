import { describe, it, expect } from 'vitest';
import { normalizeValueInDomain } from './value-normalize.js';

/**
 * Pure helper `normalizeValueInDomain(value, domain)` maps a raw metric value into
 * `[0, 1]` using the shared domain `[lo, hi]`. Used by both the topology metric
 * mapper (to produce a value the dag-map color palette consumes) and the heatmap
 * grid (to produce the same stable color domain per AC5 + ADR-m-E21-06-02).
 *
 * Contract:
 *   - `lo < hi`: standard linear map; value <= lo → 0; value >= hi → 1; in-between
 *     → linear fraction.
 *   - `lo === hi` (degenerate single-value domain): returns 0.5 for finite values
 *     so the single-value case has a stable midpoint color.
 *   - `lo > hi` or any non-finite value / domain bound: returns null (caller falls
 *     back to the "no metric" baseline).
 *   - non-finite input value (NaN, ±Infinity, null, undefined): returns null.
 */

describe('normalizeValueInDomain — happy paths', () => {
	it('maps values to [0, 1] inside a standard domain', () => {
		expect(normalizeValueInDomain(0, [0, 1])).toBe(0);
		expect(normalizeValueInDomain(1, [0, 1])).toBe(1);
		expect(normalizeValueInDomain(0.5, [0, 1])).toBe(0.5);
	});

	it('clamps values below lo to 0', () => {
		expect(normalizeValueInDomain(-10, [0, 1])).toBe(0);
	});

	it('clamps values above hi to 1', () => {
		expect(normalizeValueInDomain(10, [0, 1])).toBe(1);
	});

	it('handles non-unit domain linearly', () => {
		expect(normalizeValueInDomain(5, [0, 10])).toBe(0.5);
		expect(normalizeValueInDomain(25, [0, 100])).toBe(0.25);
	});

	it('handles negative lo correctly', () => {
		expect(normalizeValueInDomain(0, [-10, 10])).toBe(0.5);
	});
});

describe('normalizeValueInDomain — degenerate domain', () => {
	it('lo === hi: returns 0.5 for any finite value', () => {
		expect(normalizeValueInDomain(0, [5, 5])).toBe(0.5);
		expect(normalizeValueInDomain(5, [5, 5])).toBe(0.5);
		expect(normalizeValueInDomain(100, [5, 5])).toBe(0.5);
	});

	it('lo === hi: returns null for non-finite value', () => {
		expect(normalizeValueInDomain(NaN, [5, 5])).toBeNull();
	});
});

describe('normalizeValueInDomain — invalid input', () => {
	it('lo > hi: returns null', () => {
		expect(normalizeValueInDomain(5, [10, 0])).toBeNull();
	});

	it('non-finite domain bound: returns null', () => {
		expect(normalizeValueInDomain(5, [NaN, 10])).toBeNull();
		expect(normalizeValueInDomain(5, [0, Infinity])).toBeNull();
	});

	it('non-finite value: returns null', () => {
		expect(normalizeValueInDomain(NaN, [0, 1])).toBeNull();
		expect(normalizeValueInDomain(Infinity, [0, 1])).toBeNull();
		expect(normalizeValueInDomain(-Infinity, [0, 1])).toBeNull();
	});

	it('null / undefined value: returns null', () => {
		expect(normalizeValueInDomain(null, [0, 1])).toBeNull();
		expect(normalizeValueInDomain(undefined, [0, 1])).toBeNull();
	});
});
