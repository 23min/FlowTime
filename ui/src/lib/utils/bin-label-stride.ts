/**
 * Bin-axis label stride selector — m-E21-06 AC11.
 *
 * Picks an integer stride (in bins) so that labels on the heatmap's top axis land on
 * "nice" multiples of real time and sit roughly ~80–100 px apart across the typical
 * viewport width.
 *
 * Rationale: we don't take a viewport width, because the heatmap's column width is the
 * only dimension that determines label density. A stride of N bins renders one label
 * every `N × columnPixelWidth` pixels; picking N such that this product is ~100 px keeps
 * ~8–12 labels visible across a typical 800–1200 px plot.
 *
 * "Nice" stride families round to human-readable time (hourly / daily / weekly ticks).
 *
 * Defensive fallback: degenerate inputs (non-finite, non-positive column width or bin size,
 * unknown unit) return stride = 1. This prevents divide-by-zero / infinite-stride from
 * leaking into layout math downstream.
 */

export type BinUnit = 'seconds' | 'minutes' | 'hours' | 'days';

const TARGET_PIXELS_PER_LABEL = 100;

/**
 * Nice stride ladders, expressed in *bins* per ladder step, parameterized by the bin
 * unit. Each entry is a multiplier of the bin size that lands on a humanly-nice time
 * tick (hourly, daily, etc.).
 *
 * Seconds: step up through minutes, quarter-hours, halves, hours, 2/6/12h, days.
 * Minutes: step up through quarters, halves, hours, 2/3/6/12h, days, weeks.
 * Hours:   step up through hours, quarters of a day, days, weeks.
 * Days:    step up through days, weeks, fortnights, months.
 *
 * These are the *step sizes in units*, not in bins. The helper converts to bins via
 * `step / binSize` (rounded up to the nearest multiple).
 */
const NICE_STEPS: Record<BinUnit, number[]> = {
	seconds: [1, 5, 10, 15, 30, 60, 120, 300, 600, 1800, 3600, 7200, 21_600, 43_200, 86_400],
	minutes: [1, 2, 5, 10, 15, 30, 60, 120, 180, 360, 720, 1440, 2880, 10_080],
	hours: [1, 2, 3, 6, 12, 24, 48, 72, 168, 336, 720],
	days: [1, 2, 7, 14, 30, 60, 90],
};

/**
 * Pick a bin-stride for the top-axis label grid.
 *
 * @param columnPixelWidth  Rendered width of a single bin column in CSS pixels.
 * @param binSize           Bin size in the given unit (must be a positive finite number).
 * @param binUnit           One of 'seconds' | 'minutes' | 'hours' | 'days'.
 * @param maxBins           Optional upper bound on the returned stride. Callers should pass
 *                           the total bin count so strides > binCount do not produce zero
 *                           labels on short runs.
 *
 * @returns An integer stride ≥ 1.
 */
export function pickBinLabelStride(
	columnPixelWidth: number,
	binSize: number,
	binUnit: BinUnit,
	maxBins?: number
): number {
	// Defensive fallback for degenerate inputs.
	if (!Number.isFinite(columnPixelWidth) || columnPixelWidth <= 0) return 1;
	if (!Number.isFinite(binSize) || binSize <= 0) return 1;
	const ladder = NICE_STEPS[binUnit];
	if (!ladder) return 1;

	// Target stride (in bins) that gets us close to TARGET_PIXELS_PER_LABEL px per label.
	const targetBins = TARGET_PIXELS_PER_LABEL / columnPixelWidth;

	// Convert each nice step (in units) into a stride in bins, rounding up to at least 1.
	// Keep unique strides so we don't evaluate duplicates.
	const candidateStrides = new Set<number>();
	for (const step of ladder) {
		const bins = Math.max(1, Math.round(step / binSize));
		candidateStrides.add(bins);
	}

	// Pick the candidate closest to targetBins. Tie-breaker: prefer the larger stride so
	// axes don't get crowded when columns are unusually wide.
	let best = 1;
	let bestDelta = Math.abs(1 - targetBins);
	for (const c of candidateStrides) {
		const delta = Math.abs(c - targetBins);
		if (delta < bestDelta || (delta === bestDelta && c > best)) {
			best = c;
			bestDelta = delta;
		}
	}

	// Clamp to [1, maxBins] when maxBins is supplied.
	// maxBins < 1 (0, negative, non-finite) is treated as "no data" → stride 1 fallback.
	if (maxBins !== undefined && Number.isFinite(maxBins)) {
		if (maxBins < 1) return 1;
		if (best > maxBins) best = Math.floor(maxBins);
	}

	return Math.max(1, best);
}
