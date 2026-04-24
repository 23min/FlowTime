import { describe, it, expect } from 'vitest';
import {
	computeSharedColorDomain,
	bucketFromDomain,
	type ColorDomainCell,
	type BucketLabel,
} from './shared-color-domain.js';

/**
 * Shared color-scale domain helper — m-E21-06 AC5 + AC14 suite #2.
 *
 * `computeSharedColorDomain(cells, options)` returns `[min, p99-clipped max]` over the
 * cells that survive the state + class filters, or `null` when the filtered set is empty.
 *
 * Clipping policy: values above the 99th percentile are clipped to the percentile value
 * so the domain does not stretch to accommodate outliers. Values below the 1st percentile
 * are NOT clipped — the low end stays at min, so a real zero stays visible as the lightest
 * shade.
 *
 * Exclusion policy:
 *   - no-data-for-bin and metric-undefined-for-node cells are never in the domain.
 *   - Cells whose class set is disjoint from `options.classFilter` are excluded (set membership).
 *     `classFilter === undefined` or empty-set means "no filter, include everything".
 *   - `options.excludeNonObserved` defaults to true and is the documented name of the
 *     "drop no-data" behaviour. Passing `false` is reserved for future debug paths and
 *     currently results in all cells being considered (with NaN/Infinity still dropped —
 *     those are never numerically useful).
 *
 * Domain shape: `[min, clippedMax]` where both are finite and `clippedMax >= min`.
 */

function obs(v: number, classes?: string[]): ColorDomainCell {
	return { value: v, state: 'observed', classes };
}

function missing(classes?: string[]): ColorDomainCell {
	return { value: null, state: 'no-data-for-bin', classes };
}

function undef(classes?: string[]): ColorDomainCell {
	return { value: null, state: 'metric-undefined-for-node', classes };
}

describe('computeSharedColorDomain — happy paths', () => {
	it('all-observed → [min, p99] — p99 sits slightly below raw max for small N via linear interpolation', () => {
		const cells = [obs(0.1), obs(0.5), obs(0.9)];
		const domain = computeSharedColorDomain(cells)!;
		expect(domain[0]).toBe(0.1);
		// With n=3, 99p via linear interpolation lands just under 0.9 (~0.892).
		expect(domain[1]).toBeGreaterThan(0.5);
		expect(domain[1]).toBeLessThanOrEqual(0.9);
	});

	it('domain includes zero as min when zero is present', () => {
		const cells = [obs(0), obs(0.5), obs(1)];
		const domain = computeSharedColorDomain(cells)!;
		expect(domain[0]).toBe(0);
		expect(domain[1]).toBeGreaterThan(0.5);
		expect(domain[1]).toBeLessThanOrEqual(1);
	});

	it('single observed value → [v, v] degenerate domain', () => {
		const cells = [obs(0.42)];
		expect(computeSharedColorDomain(cells)).toEqual([0.42, 0.42]);
	});

	it('all-equal observed values → [v, v] degenerate domain', () => {
		const cells = [obs(0.5), obs(0.5), obs(0.5)];
		expect(computeSharedColorDomain(cells)).toEqual([0.5, 0.5]);
	});

	it('negative values produce a negative-anchored domain', () => {
		const cells = [obs(-5), obs(-2), obs(0)];
		const domain = computeSharedColorDomain(cells)!;
		expect(domain[0]).toBe(-5);
		expect(domain[1]).toBeGreaterThan(-2);
		expect(domain[1]).toBeLessThanOrEqual(0);
	});
});

describe('computeSharedColorDomain — exclusions', () => {
	it('no-data-for-bin cells are excluded from the domain', () => {
		const cells = [obs(0.1), missing(), obs(0.9)];
		const domain = computeSharedColorDomain(cells)!;
		expect(domain[0]).toBe(0.1);
		expect(domain[1]).toBeGreaterThan(0.1);
		expect(domain[1]).toBeLessThanOrEqual(0.9);
	});

	it('metric-undefined-for-node cells are excluded from the domain', () => {
		const cells = [obs(0.1), undef(), obs(0.9)];
		const domain = computeSharedColorDomain(cells)!;
		expect(domain[0]).toBe(0.1);
		expect(domain[1]).toBeGreaterThan(0.1);
		expect(domain[1]).toBeLessThanOrEqual(0.9);
	});

	it('mixed exclusions are all dropped', () => {
		const cells = [obs(0.1), missing(), undef(), obs(0.9), missing()];
		const domain = computeSharedColorDomain(cells)!;
		expect(domain[0]).toBe(0.1);
		expect(domain[1]).toBeGreaterThan(0.1);
		expect(domain[1]).toBeLessThanOrEqual(0.9);
	});

	it('all-missing → null (caller must fall back)', () => {
		const cells = [missing(), missing(), undef()];
		expect(computeSharedColorDomain(cells)).toBeNull();
	});

	it('empty input → null', () => {
		expect(computeSharedColorDomain([])).toBeNull();
	});

	it('NaN and Infinity in an observed slot are dropped defensively', () => {
		// Normally the upstream classifier prevents this, but the helper is robust on its own.
		const cells = [obs(NaN), obs(Infinity), obs(-Infinity), obs(0.5)];
		expect(computeSharedColorDomain(cells)).toEqual([0.5, 0.5]);
	});
});

describe('computeSharedColorDomain — 99th-percentile clipping', () => {
	it('values above the 99p are clipped down to the 99p value', () => {
		// 100 cells: 99 at 0.5, 1 outlier at 10. 99p ≈ 0.5 (or close to it), so max clamps.
		const cells: ColorDomainCell[] = [];
		for (let i = 0; i < 99; i++) cells.push(obs(0.5));
		cells.push(obs(10));
		const domain = computeSharedColorDomain(cells);
		expect(domain).not.toBeNull();
		expect(domain![1]).toBeLessThan(10); // outlier pushed down
		expect(domain![0]).toBe(0.5);
	});

	it('clip percentile is configurable', () => {
		// Using 50p ≈ the median; outliers > median are clipped to the median.
		const cells: ColorDomainCell[] = [];
		for (let i = 0; i < 101; i++) cells.push(obs(i));
		const domain = computeSharedColorDomain(cells, { clipPercentile: 50 });
		expect(domain).not.toBeNull();
		expect(domain![1]).toBeLessThanOrEqual(51);
		expect(domain![1]).toBeGreaterThanOrEqual(49);
	});

	it('fewer than 100 cells — clipping uses linear-interpolated percentile', () => {
		// 10 cells with values 0..9. 99p of 0..9 is ~8.91 by linear interpolation.
		const cells: ColorDomainCell[] = [];
		for (let i = 0; i < 10; i++) cells.push(obs(i));
		const domain = computeSharedColorDomain(cells);
		expect(domain).not.toBeNull();
		expect(domain![0]).toBe(0);
		expect(domain![1]).toBeGreaterThanOrEqual(8.5);
		expect(domain![1]).toBeLessThanOrEqual(9);
	});

	it('exactly 100 cells — 99p is the 99th element (index 98 or 99 by method)', () => {
		const cells: ColorDomainCell[] = [];
		for (let i = 0; i < 100; i++) cells.push(obs(i));
		const domain = computeSharedColorDomain(cells);
		expect(domain).not.toBeNull();
		expect(domain![0]).toBe(0);
		expect(domain![1]).toBeGreaterThanOrEqual(97);
		expect(domain![1]).toBeLessThanOrEqual(99);
	});

	it('clipPercentile = 100 → no clipping (max == raw max)', () => {
		const cells = [obs(0), obs(0.5), obs(100)];
		const domain = computeSharedColorDomain(cells, { clipPercentile: 100 });
		expect(domain).toEqual([0, 100]);
	});

	it('clipPercentile = 0 → clips to minimum (degenerate domain)', () => {
		const cells = [obs(0), obs(0.5), obs(1)];
		const domain = computeSharedColorDomain(cells, { clipPercentile: 0 });
		expect(domain![0]).toBe(0);
		expect(domain![1]).toBe(0);
	});

	it('clipPercentile outside [0, 100] falls back to 99', () => {
		const cells = [obs(0.1), obs(0.9)];
		const expected = computeSharedColorDomain(cells, { clipPercentile: 99 });
		expect(computeSharedColorDomain(cells, { clipPercentile: -5 })).toEqual(expected);
		expect(computeSharedColorDomain(cells, { clipPercentile: 150 })).toEqual(expected);
		expect(computeSharedColorDomain(cells, { clipPercentile: NaN })).toEqual(expected);
	});

	it('clippedMax is always >= min even for degenerate single-value input', () => {
		const cells = [obs(5), obs(5), obs(5)];
		const [lo, hi] = computeSharedColorDomain(cells)!;
		expect(hi).toBeGreaterThanOrEqual(lo);
	});
});

describe('computeSharedColorDomain — class filter', () => {
	it('undefined classFilter includes all cells', () => {
		const cells = [obs(0.1, ['web']), obs(0.9, ['batch'])];
		const domain = computeSharedColorDomain(cells)!;
		expect(domain[0]).toBe(0.1);
		expect(domain[1]).toBeGreaterThan(0.1);
		expect(domain[1]).toBeLessThanOrEqual(0.9);
	});

	it('empty classFilter is treated as no filter', () => {
		const cells = [obs(0.1, ['web']), obs(0.9, ['batch'])];
		const domain = computeSharedColorDomain(cells, { classFilter: new Set() })!;
		expect(domain[0]).toBe(0.1);
		expect(domain[1]).toBeGreaterThan(0.1);
		expect(domain[1]).toBeLessThanOrEqual(0.9);
	});

	it('non-empty classFilter keeps only cells whose classes intersect the filter', () => {
		const cells = [obs(0.1, ['web']), obs(0.5, ['batch']), obs(0.9, ['web', 'batch'])];
		const domain = computeSharedColorDomain(cells, { classFilter: new Set(['web']) })!;
		expect(domain[0]).toBe(0.1);
		expect(domain[1]).toBeGreaterThan(0.1);
		expect(domain[1]).toBeLessThanOrEqual(0.9);
	});

	it('cells without class tags pass through regardless of filter (aggregate rows)', () => {
		// A node-level aggregate cell has no class tag; leaving it in the domain matches
		// the topology filter semantics (class filter sums per-class; aggregate row still
		// contributes to max/min of the shared domain).
		const cells = [obs(0.1), obs(0.5, ['web']), obs(0.9, ['batch'])];
		const domain = computeSharedColorDomain(cells, { classFilter: new Set(['web']) })!;
		expect(domain[0]).toBe(0.1);
		expect(domain[1]).toBeGreaterThan(0.1);
		expect(domain[1]).toBeLessThanOrEqual(0.5);
	});

	it('classFilter excludes everything → null', () => {
		const cells = [obs(0.5, ['web']), obs(0.7, ['web'])];
		expect(
			computeSharedColorDomain(cells, { classFilter: new Set(['ghost']) })
		).toBeNull();
	});
});

describe('computeSharedColorDomain — excludeNonObserved option', () => {
	it('default excludeNonObserved: true drops missing/undef', () => {
		const cells = [obs(0.1), missing(), undef(), obs(0.9)];
		const domain = computeSharedColorDomain(cells, { excludeNonObserved: true })!;
		expect(domain[0]).toBe(0.1);
		expect(domain[1]).toBeGreaterThan(0.1);
		expect(domain[1]).toBeLessThanOrEqual(0.9);
	});

	it('excludeNonObserved: false still drops NaN/Infinity (never numerically useful)', () => {
		const cells = [obs(0.1), obs(NaN), obs(0.9)];
		const domain = computeSharedColorDomain(cells, { excludeNonObserved: false })!;
		expect(domain[0]).toBe(0.1);
		expect(domain[1]).toBeGreaterThan(0.1);
		expect(domain[1]).toBeLessThanOrEqual(0.9);
	});
});

describe('bucketFromDomain — coarse 3-bucket classifier', () => {
	it('values in the low third → low', () => {
		expect(bucketFromDomain(0.1, [0, 1])).toBe<BucketLabel>('low');
		expect(bucketFromDomain(0.33, [0, 1])).toBe<BucketLabel>('low');
	});

	it('values in the middle third → mid', () => {
		expect(bucketFromDomain(0.5, [0, 1])).toBe<BucketLabel>('mid');
		expect(bucketFromDomain(0.66, [0, 1])).toBe<BucketLabel>('mid');
	});

	it('values in the upper third → high', () => {
		expect(bucketFromDomain(0.8, [0, 1])).toBe<BucketLabel>('high');
		expect(bucketFromDomain(1.0, [0, 1])).toBe<BucketLabel>('high');
	});

	it('values below domain.min clamp to low', () => {
		expect(bucketFromDomain(-5, [0, 1])).toBe<BucketLabel>('low');
	});

	it('values above domain.max clamp to high', () => {
		expect(bucketFromDomain(10, [0, 1])).toBe<BucketLabel>('high');
	});

	it('degenerate domain (min == max) → mid for any value', () => {
		expect(bucketFromDomain(5, [5, 5])).toBe<BucketLabel>('mid');
	});

	it('non-finite value → "no-data" marker so caller can branch without re-classifying', () => {
		expect(bucketFromDomain(NaN, [0, 1])).toBe<BucketLabel>('no-data');
		expect(bucketFromDomain(Infinity, [0, 1])).toBe<BucketLabel>('no-data');
		expect(bucketFromDomain(-Infinity, [0, 1])).toBe<BucketLabel>('no-data');
	});

	it('null / undefined value → "no-data"', () => {
		expect(bucketFromDomain(null, [0, 1])).toBe<BucketLabel>('no-data');
		expect(bucketFromDomain(undefined, [0, 1])).toBe<BucketLabel>('no-data');
	});
});
