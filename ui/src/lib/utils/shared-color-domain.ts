/**
 * Shared color-scale domain helper — m-E21-06 AC5 + ADR-m-E21-06-02.
 *
 * Both topology and heatmap compute their metric color mapping from the same
 * **full-window, 99th-percentile-clipped** domain so "bright red at (N, T)" on the
 * heatmap matches "bright red on node N" on topology when the scrubber is at bin T.
 *
 * The helper consumes a flat list of `ColorDomainCell` (each tagged with its cell state
 * and optional class set) plus an options object, applies the exclusion rules below, and
 * returns the numeric domain `[min, clippedMax]`, or `null` when the filtered cell set is
 * empty.
 *
 * Exclusion policy:
 *   1. Non-observed cells (`no-data-for-bin`, `metric-undefined-for-node`) are dropped
 *      when `excludeNonObserved` is true (default). This is the documented behaviour per
 *      AC5; passing `false` is reserved for debug paths.
 *   2. Non-finite numeric values (NaN / Infinity / -Infinity) are ALWAYS dropped — they
 *      are never legitimate domain contributors regardless of cell state.
 *   3. When `classFilter` is a non-empty set, cells are kept only if their `classes` tag
 *      intersects the filter. Cells without a `classes` tag represent node-level aggregate
 *      values and pass through unchanged (matching topology's existing filter semantics
 *      where aggregates still contribute to the shared domain).
 *
 * Clipping policy:
 *   - Computes the `clipPercentile`-th percentile of the surviving values via linear
 *     interpolation (type-7, the NumPy / SciPy default). The returned max is the
 *     percentile value, so any single outlier above it does not stretch the domain.
 *   - `clipPercentile` must be in `[0, 100]`; out-of-range or non-finite values fall back
 *     to 99.
 *   - The min is the raw minimum — we do not clip the low end so a genuine zero stays
 *     visible as the lightest shade.
 *
 * The companion `bucketFromDomain(v, [lo, hi])` helper maps a cell value into one of
 * `'low' | 'mid' | 'high' | 'no-data'`. Playwright spec #11 uses it as the assertion
 * surface (via a `data-value-bucket` attribute on each cell) so correctness tests do not
 * depend on CSS-color string parsing.
 */

export type CellStateForDomain =
	| 'observed'
	| 'no-data-for-bin'
	| 'metric-undefined-for-node';

export interface ColorDomainCell {
	/** Raw value; null / undefined are legal for non-observed states. */
	value: number | null | undefined;
	state: CellStateForDomain;
	/** Optional class tags for the cell. Undefined means "aggregate / node-level". */
	classes?: ReadonlyArray<string>;
}

export interface ColorDomainOptions {
	classFilter?: ReadonlySet<string>;
	excludeNonObserved?: boolean;
	/** Percentile in [0, 100]. Default 99. */
	clipPercentile?: number;
}

const DEFAULT_CLIP = 99;

/**
 * Compute the shared color domain. Returns `null` when no cells survive filtering.
 */
export function computeSharedColorDomain(
	cells: ReadonlyArray<ColorDomainCell>,
	options: ColorDomainOptions = {}
): [number, number] | null {
	const excludeNonObserved = options.excludeNonObserved !== false;
	const classFilter = options.classFilter;
	const hasClassFilter = classFilter !== undefined && classFilter.size > 0;

	const values: number[] = [];
	for (const c of cells) {
		if (excludeNonObserved && c.state !== 'observed') continue;
		if (c.value === null || c.value === undefined) continue;
		if (!Number.isFinite(c.value)) continue;
		if (hasClassFilter && c.classes !== undefined) {
			let hit = false;
			for (const cls of c.classes) {
				if (classFilter!.has(cls)) {
					hit = true;
					break;
				}
			}
			if (!hit) continue;
		}
		values.push(c.value);
	}

	if (values.length === 0) return null;

	values.sort((a, b) => a - b);
	const min = values[0];

	// Pick clip percentile with defensive fallback.
	const rawClip = options.clipPercentile;
	const clip =
		rawClip !== undefined && Number.isFinite(rawClip) && rawClip >= 0 && rawClip <= 100
			? rawClip
			: DEFAULT_CLIP;

	// linearPercentile on a sorted ascending array returns a value in [sorted[0], sorted[-1]],
	// so clippedMax is always >= min by construction. No extra clamp is needed.
	const clippedMax = linearPercentile(values, clip);
	return [min, clippedMax];
}

/**
 * Linear-interpolated percentile over a pre-sorted ascending array (type-7 — NumPy /
 * SciPy default; also what R's `quantile(x, type=7)` returns).
 *
 * Pre-condition: `sorted.length >= 1`.
 */
function linearPercentile(sorted: number[], percentile: number): number {
	// Pre-condition: percentile is already validated to lie in [0, 100] by the caller.
	const n = sorted.length;
	if (n === 1) return sorted[0];
	if (percentile === 0) return sorted[0];
	if (percentile === 100) return sorted[n - 1];
	const rank = (percentile / 100) * (n - 1);
	const lo = Math.floor(rank);
	const hi = Math.ceil(rank);
	if (lo === hi) return sorted[lo];
	const weight = rank - lo;
	return sorted[lo] * (1 - weight) + sorted[hi] * weight;
}

export type BucketLabel = 'low' | 'mid' | 'high' | 'no-data';

/**
 * Map a value into a coarse 3-bucket label given the shared domain. Returns `'no-data'`
 * when the value is null / undefined / non-finite.
 *
 * Playwright spec #11 asserts this via a `data-value-bucket` attribute on each rendered
 * cell; the vitest suite above enforces the math end-to-end so tests are independent of
 * the color palette.
 *
 * Bucketing thirds:
 *   [min, min + range/3)  → low
 *   [min + range/3, min + 2*range/3) → mid
 *   [min + 2*range/3, max] → high
 *
 * Degenerate domain (min == max): returns 'mid' for any finite value so the single-value
 * case has a stable label.
 */
export function bucketFromDomain(
	value: number | null | undefined,
	domain: readonly [number, number]
): BucketLabel {
	if (value === null || value === undefined) return 'no-data';
	if (!Number.isFinite(value)) return 'no-data';
	const [lo, hi] = domain;
	if (hi === lo) return 'mid';
	if (value <= lo) return 'low';
	if (value >= hi) return 'high';
	const range = hi - lo;
	const t = (value - lo) / range;
	if (t < 1 / 3) return 'low';
	if (t < 2 / 3) return 'mid';
	return 'high';
}
