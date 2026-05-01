import { describe, it, expect } from 'vitest';
import {
	defaultSearchBounds,
	validateSearchInterval,
	formatResidual,
} from './goal-seek-helpers.js';

describe('defaultSearchBounds', () => {
	it('returns 0.5× / 2× baseline for positive baseline', () => {
		expect(defaultSearchBounds(10)).toEqual({ lo: 5, hi: 20 });
	});

	it('handles fractional positive baseline', () => {
		expect(defaultSearchBounds(2.5)).toEqual({ lo: 1.25, hi: 5 });
	});

	it('returns a symmetric span around 0 when baseline is 0', () => {
		// 0.5 × 0 = 0 and 2 × 0 = 0 would collapse the interval; fall back to [-1, 1]
		expect(defaultSearchBounds(0)).toEqual({ lo: -1, hi: 1 });
	});

	it('returns a symmetric span around 0 for non-finite baseline', () => {
		expect(defaultSearchBounds(NaN)).toEqual({ lo: -1, hi: 1 });
		expect(defaultSearchBounds(Infinity)).toEqual({ lo: -1, hi: 1 });
		expect(defaultSearchBounds(-Infinity)).toEqual({ lo: -1, hi: 1 });
	});

	it('swaps to [2×, 0.5×] for negative baseline so lo < hi', () => {
		// Negative baseline: 0.5× = -5, 2× = -20 — hi < lo unless we swap.
		// Return [2× baseline, 0.5× baseline] so lo < hi is preserved.
		expect(defaultSearchBounds(-10)).toEqual({ lo: -20, hi: -5 });
	});
});

describe('validateSearchInterval', () => {
	it('accepts lo < hi with finite numbers', () => {
		expect(validateSearchInterval({ lo: 10, hi: 100 })).toEqual({ ok: true });
	});

	it('accepts negative interval with lo < hi', () => {
		expect(validateSearchInterval({ lo: -10, hi: -5 })).toEqual({ ok: true });
	});

	it('rejects when lo === hi', () => {
		const r = validateSearchInterval({ lo: 10, hi: 10 });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.reason).toMatch(/less than/i);
	});

	it('rejects when lo > hi', () => {
		const r = validateSearchInterval({ lo: 20, hi: 10 });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.reason).toMatch(/less than/i);
	});

	it('rejects non-finite lo', () => {
		const r = validateSearchInterval({ lo: NaN, hi: 10 });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.reason).toMatch(/finite number/i);
	});

	it('rejects non-finite hi', () => {
		const r = validateSearchInterval({ lo: 10, hi: Infinity });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.reason).toMatch(/finite number/i);
	});

	it('rejects when lo is missing/undefined', () => {
		// @ts-expect-error — missing lo
		const r = validateSearchInterval({ hi: 10 });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.reason).toMatch(/finite number/i);
	});

	it('rejects when hi is missing/undefined', () => {
		// @ts-expect-error — missing hi
		const r = validateSearchInterval({ lo: 10 });
		expect(r.ok).toBe(false);
		if (!r.ok) expect(r.reason).toMatch(/finite number/i);
	});
});

describe('formatResidual', () => {
	it('returns em-dash for non-finite', () => {
		expect(formatResidual(NaN)).toBe('—');
		expect(formatResidual(Infinity)).toBe('—');
	});

	it('formats tiny residuals in scientific notation', () => {
		expect(formatResidual(1e-8)).toMatch(/e/);
		expect(formatResidual(-2.5e-9)).toMatch(/e/);
	});

	it('formats sub-1 residuals with four decimals', () => {
		expect(formatResidual(0.1234)).toBe('0.1234');
		expect(formatResidual(-0.9999)).toBe('-0.9999');
	});

	it('formats mid-range residuals with two decimals', () => {
		expect(formatResidual(12.5)).toBe('12.50');
		expect(formatResidual(-99.99)).toBe('-99.99');
	});

	it('formats large residuals without decimals', () => {
		expect(formatResidual(1234)).toBe('1234');
		expect(formatResidual(-9999.6)).toBe('-10000');
	});

	it('boundary: zero formats with four decimals', () => {
		expect(formatResidual(0)).toBe('0.0000');
	});
});
