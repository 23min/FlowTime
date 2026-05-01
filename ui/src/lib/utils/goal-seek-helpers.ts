/**
 * Pure helpers for the Goal Seek analysis surface.
 *
 * Separated from Svelte components so every branch can be unit-tested
 * without a DOM or reactive runtime.
 */

export interface SearchBounds {
	lo: number;
	hi: number;
}

/**
 * Default `[searchLo, searchHi]` for a parameter given its baseline.
 *
 * Returns `[0.5 × baseline, 2 × baseline]` for a positive baseline — a
 * symmetric-in-log-space bracket around the baseline. For zero / non-finite
 * baselines, falls back to `[-1, 1]` because the multiplicative default
 * collapses to a zero-width interval. For negative baselines, swaps the
 * endpoints so `lo < hi` holds in the negative-number line
 * (`[2 × baseline, 0.5 × baseline]`).
 */
export function defaultSearchBounds(baseline: number): SearchBounds {
	if (!isFinite(baseline) || baseline === 0) {
		return { lo: -1, hi: 1 };
	}
	if (baseline > 0) {
		return { lo: 0.5 * baseline, hi: 2 * baseline };
	}
	return { lo: 2 * baseline, hi: 0.5 * baseline };
}

export type ValidationResult = { ok: true } | { ok: false; reason: string };

/**
 * Validate a search interval for the goal-seek runner.
 *
 * Requires both endpoints to be finite numbers and `lo < hi`.
 */
export function validateSearchInterval(input: { lo: number; hi: number }): ValidationResult {
	const lo = input?.lo;
	const hi = input?.hi;
	if (typeof lo !== 'number' || !isFinite(lo) || typeof hi !== 'number' || !isFinite(hi)) {
		return { ok: false, reason: 'searchLo and searchHi must be finite numbers.' };
	}
	if (lo >= hi) {
		return { ok: false, reason: 'searchLo must be less than searchHi.' };
	}
	return { ok: true };
}

/**
 * Format a residual (|achieved − target|) for display on the result card.
 *
 * Uses scientific notation for tiny residuals (|v| < 1e-3) so post-convergence
 * tolerance-scale numbers remain legible; progressively fewer decimals as the
 * magnitude grows.
 */
export function formatResidual(v: number): string {
	if (!isFinite(v)) return '—';
	const abs = Math.abs(v);
	if (abs !== 0 && abs < 1e-3) return v.toExponential(2);
	if (abs >= 1000) return v.toFixed(0);
	if (abs >= 1) return v.toFixed(2);
	return v.toFixed(4);
}
