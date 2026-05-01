import { describe, it, expect } from 'vitest';
import { validateOptimizeForm } from './optimize-helpers.js';

const ok = {
	paramIds: ['a'],
	bounds: { a: { lo: 1, hi: 10 } },
	metricSeriesId: 'queue',
	tolerance: 1e-4,
	maxIterations: 200,
};

describe('validateOptimizeForm — paramIds', () => {
	it('accepts one selected param with valid bounds', () => {
		expect(validateOptimizeForm(ok)).toEqual({ ok: true });
	});

	it('accepts multiple selected params', () => {
		const r = validateOptimizeForm({
			...ok,
			paramIds: ['a', 'b'],
			bounds: { a: { lo: 1, hi: 10 }, b: { lo: 0, hi: 5 } },
		});
		expect(r.ok).toBe(true);
	});

	it('rejects empty paramIds', () => {
		const r = validateOptimizeForm({ ...ok, paramIds: [] });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.paramIds).toMatch(/at least one/i);
	});
});

describe('validateOptimizeForm — bounds', () => {
	it('flags a selected param with missing bounds entry', () => {
		const r = validateOptimizeForm({ ...ok, bounds: {} });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.bounds?.a).toMatch(/missing/i);
	});

	it('flags non-finite lo', () => {
		const r = validateOptimizeForm({
			...ok,
			bounds: { a: { lo: NaN, hi: 10 } },
		});
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.bounds?.a).toMatch(/finite/i);
	});

	it('flags non-finite hi', () => {
		const r = validateOptimizeForm({
			...ok,
			bounds: { a: { lo: 1, hi: Infinity } },
		});
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.bounds?.a).toMatch(/finite/i);
	});

	it('flags lo === hi', () => {
		const r = validateOptimizeForm({
			...ok,
			bounds: { a: { lo: 5, hi: 5 } },
		});
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.bounds?.a).toMatch(/less than/i);
	});

	it('flags lo > hi', () => {
		const r = validateOptimizeForm({
			...ok,
			bounds: { a: { lo: 10, hi: 1 } },
		});
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.bounds?.a).toMatch(/less than/i);
	});

	it('reports per-param errors when some bounds invalid and others valid', () => {
		const r = validateOptimizeForm({
			...ok,
			paramIds: ['a', 'b'],
			bounds: { a: { lo: 1, hi: 10 }, b: { lo: 5, hi: 5 } },
		});
		expect(r.ok).toBe(false);
		if (!r.ok) {
			expect(r.errors.bounds?.a).toBeUndefined();
			expect(r.errors.bounds?.b).toMatch(/less than/i);
		}
	});

	it('accepts negative-range bounds where lo < hi', () => {
		const r = validateOptimizeForm({
			...ok,
			bounds: { a: { lo: -20, hi: -5 } },
		});
		expect(r.ok).toBe(true);
	});
});

describe('validateOptimizeForm — metric / advanced', () => {
	it('flags empty metricSeriesId', () => {
		const r = validateOptimizeForm({ ...ok, metricSeriesId: '' });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.metricSeriesId).toMatch(/metric/i);
	});

	it('flags whitespace-only metricSeriesId', () => {
		const r = validateOptimizeForm({ ...ok, metricSeriesId: '   ' });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.metricSeriesId).toMatch(/metric/i);
	});

	it('flags non-finite tolerance', () => {
		const r = validateOptimizeForm({ ...ok, tolerance: NaN });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.tolerance).toMatch(/positive/i);
	});

	it('flags zero tolerance', () => {
		const r = validateOptimizeForm({ ...ok, tolerance: 0 });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.tolerance).toMatch(/positive/i);
	});

	it('flags negative tolerance', () => {
		const r = validateOptimizeForm({ ...ok, tolerance: -1 });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.tolerance).toMatch(/positive/i);
	});

	it('flags non-integer maxIterations', () => {
		const r = validateOptimizeForm({ ...ok, maxIterations: 1.5 });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.maxIterations).toMatch(/integer/i);
	});

	it('flags zero maxIterations', () => {
		const r = validateOptimizeForm({ ...ok, maxIterations: 0 });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.maxIterations).toMatch(/at least 1/i);
	});

	it('flags non-finite maxIterations', () => {
		const r = validateOptimizeForm({ ...ok, maxIterations: NaN });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.errors.maxIterations).toMatch(/integer/i);
	});

	it('aggregates multiple independent errors', () => {
		const r = validateOptimizeForm({
			paramIds: [],
			bounds: {},
			metricSeriesId: '',
			tolerance: -1,
			maxIterations: 0,
		});
		expect(r.ok).toBe(false);
		if (!r.ok) {
			expect(r.errors.paramIds).toBeDefined();
			expect(r.errors.metricSeriesId).toBeDefined();
			expect(r.errors.tolerance).toBeDefined();
			expect(r.errors.maxIterations).toBeDefined();
		}
	});
});
