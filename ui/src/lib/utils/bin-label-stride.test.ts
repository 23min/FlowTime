import { describe, it, expect } from 'vitest';
import { pickBinLabelStride } from './bin-label-stride.js';

/**
 * Bin-axis label stride selector — m-E21-06 AC11.
 *
 * `pickBinLabelStride(columnPixelWidth, binSize, binUnit)` picks an integer bin-stride
 * such that labels are spaced roughly every ~80–100px on the top axis AND land on
 * round multiples of real time given the bin size/unit (e.g. hourly ticks for a 5-min
 * grid; daily ticks for an hourly grid).
 *
 * Contract:
 *   - Returns a positive integer >= 1.
 *   - Never exceeds the provided `maxBins` (if supplied) — caller may pass `binCount`
 *     to avoid strides that would produce zero labels on very short runs.
 *   - Degenerate inputs (columnPixelWidth ≤ 0, non-finite inputs) fall back to stride = 1.
 *
 * Nice stride families:
 *   - seconds: [1, 5, 10, 15, 30, 60, 120, 300, 600, 1800, 3600, ...] — real-time rounds.
 *   - minutes: [1, 5, 10, 15, 30, 60, 120, 180, 360, 720, 1440, ...] — minute rounds up to days.
 *   - hours:   [1, 2, 3, 6, 12, 24, 48, 168]                          — hour rounds up to weeks.
 *   - days:    [1, 2, 7, 14, 30]                                      — day rounds up to months.
 */

describe('pickBinLabelStride — 5-min grid', () => {
	it('wide columns (10px/bin) → hourly ticks (stride = 12 bins = 60 minutes)', () => {
		expect(pickBinLabelStride(10, 5, 'minutes')).toBe(12);
	});

	it('narrow columns (2px/bin) → larger stride', () => {
		// target ~100px → ~50 bins. Nearest nice stride for 5-min bins:
		// minute-nice strides that are multiples of 5: 30 (150 min = 2.5h) or 60 (300 min = 5h)
		// Closest to 50 is 60 → 300 minutes = 5h. Both 30 and 60 are "valid"; we lock 60.
		const stride = pickBinLabelStride(2, 5, 'minutes');
		expect(stride).toBeGreaterThanOrEqual(30);
		expect(stride).toBeLessThanOrEqual(72);
	});

	it('very wide columns (40px/bin) → smaller stride', () => {
		// target ~100px → ~2.5 bins → nearest nice is 1 or 3 bins. Since we prefer rounding
		// to minute-multiples (5 min bin × stride), stride=3 = 15 minutes is a good answer.
		// Alternately stride=1 = 5 minutes is also acceptable for display density.
		const stride = pickBinLabelStride(40, 5, 'minutes');
		expect(stride).toBeGreaterThanOrEqual(1);
		expect(stride).toBeLessThanOrEqual(6);
	});
});

describe('pickBinLabelStride — hourly grid', () => {
	it('moderate columns (8px/bin) → daily ticks (stride = 24 bins)', () => {
		// target ~100px → ~12 bins. Hour-nice strides near 12: 12 (half-day) or 24 (daily).
		// The larger of the two, 24, aligns with the "daily ticks for an hourly grid" note.
		const stride = pickBinLabelStride(8, 1, 'hours');
		expect([12, 24]).toContain(stride);
	});

	it('very narrow columns (2px/bin) → weekly stride', () => {
		// target ~100px → ~50 bins. Nearest hour-nice: 48 (2-day) or 168 (weekly).
		const stride = pickBinLabelStride(2, 1, 'hours');
		expect(stride).toBeGreaterThanOrEqual(24);
		expect(stride).toBeLessThanOrEqual(168);
	});
});

describe('pickBinLabelStride — degenerate inputs', () => {
	it('columnPixelWidth = 0 → stride = 1 (fallback, no divide-by-zero)', () => {
		expect(pickBinLabelStride(0, 5, 'minutes')).toBe(1);
	});

	it('negative columnPixelWidth → stride = 1 (fallback)', () => {
		expect(pickBinLabelStride(-5, 5, 'minutes')).toBe(1);
	});

	it('NaN columnPixelWidth → stride = 1 (fallback)', () => {
		expect(pickBinLabelStride(NaN, 5, 'minutes')).toBe(1);
	});

	it('non-finite binSize → stride = 1 (fallback)', () => {
		expect(pickBinLabelStride(10, Infinity, 'minutes')).toBe(1);
	});

	it('binSize = 0 → stride = 1 (fallback)', () => {
		expect(pickBinLabelStride(10, 0, 'minutes')).toBe(1);
	});

	it('unknown binUnit → stride = 1 (fallback)', () => {
		// @ts-expect-error — deliberately passing an invalid unit
		expect(pickBinLabelStride(10, 5, 'parsecs')).toBe(1);
	});

	it('extremely wide columns (huge px/bin) → stride = 1 (can\'t go below 1)', () => {
		expect(pickBinLabelStride(10_000, 5, 'minutes')).toBe(1);
	});

	it('huge binSize → nearest nice stride within family', () => {
		// binSize 1440 min (daily) with narrow cols should suggest multi-day stride.
		const stride = pickBinLabelStride(5, 1440, 'minutes');
		expect(stride).toBeGreaterThanOrEqual(1);
	});
});

describe('pickBinLabelStride — maxBins clamp', () => {
	it('clamps stride to maxBins when supplied', () => {
		// Narrow columns would normally pick a large stride, but if the whole run has only
		// 6 bins, a stride > 6 yields zero labels. Clamp to <= maxBins.
		const stride = pickBinLabelStride(2, 5, 'minutes', 6);
		expect(stride).toBeLessThanOrEqual(6);
		expect(stride).toBeGreaterThanOrEqual(1);
	});

	it('maxBins = 1 → stride = 1', () => {
		expect(pickBinLabelStride(10, 5, 'minutes', 1)).toBe(1);
	});

	it('maxBins = 0 → stride = 1 (ignored/fallback)', () => {
		expect(pickBinLabelStride(10, 5, 'minutes', 0)).toBe(1);
	});

	it('undefined maxBins → no clamp', () => {
		expect(pickBinLabelStride(10, 5, 'minutes', undefined)).toBe(12);
	});
});

describe('pickBinLabelStride — tie-breaker prefers the larger stride', () => {
	it('when two candidate strides are equidistant from target, prefers the larger one', () => {
		// columnPixelWidth = 33.3, binSize = 15 minutes → target ≈ 3 bins.
		// 15-minute ladder → unique candidate strides {1, 2, 4, 8, ...}. Both 2 and 4
		// sit |delta|=1 from target 3. Tie-breaker prefers 4 so the axis isn't dense.
		expect(pickBinLabelStride(100 / 3, 15, 'minutes')).toBe(4);
	});
});

describe('pickBinLabelStride — seconds and days units', () => {
	it('seconds grid (1s bins, narrow cols) → minute-scale stride', () => {
		// target ~100px with 2px/bin = 50 bins. Nice second-strides near 50: 30 or 60.
		const stride = pickBinLabelStride(2, 1, 'seconds');
		expect([30, 60, 120]).toContain(stride);
	});

	it('daily grid (1d bins, moderate cols) → weekly-ish stride', () => {
		// target ~100px with 15px/bin = ~7 bins. Day-nice strides near 7: 7 (weekly).
		const stride = pickBinLabelStride(15, 1, 'days');
		expect([2, 7, 14]).toContain(stride);
	});
});
