/**
 * Pure helpers for the Optimize analysis surface.
 *
 * Separated from Svelte components so every branch can be unit-tested
 * without a DOM or reactive runtime, and from `analysis-helpers.ts` so
 * optimize-specific form/validation logic doesn't pile into the shared
 * cross-surface module.
 */

export interface OptimizeBounds {
	lo: number;
	hi: number;
}

export interface OptimizeFormInput {
	paramIds: string[];
	bounds: Record<string, OptimizeBounds>;
	metricSeriesId: string;
	tolerance: number;
	maxIterations: number;
}

export interface OptimizeFormErrors {
	paramIds?: string;
	bounds?: Record<string, string>;
	metricSeriesId?: string;
	tolerance?: string;
	maxIterations?: string;
}

export type OptimizeValidationResult =
	| { ok: true }
	| { ok: false; errors: OptimizeFormErrors };

/**
 * Validate every required field on the Optimize form.
 *
 * Returns an error map keyed by field (with per-param errors under
 * `bounds[paramId]`) so the caller can render inline hints next to the
 * offending input. `ok: true` is only returned when every field is valid.
 */
export function validateOptimizeForm(input: OptimizeFormInput): OptimizeValidationResult {
	const errors: OptimizeFormErrors = {};

	if (!Array.isArray(input.paramIds) || input.paramIds.length === 0) {
		errors.paramIds = 'Select at least one parameter to optimize.';
	}

	const boundsErrors: Record<string, string> = {};
	for (const id of input.paramIds ?? []) {
		const b = input.bounds?.[id];
		if (!b) {
			boundsErrors[id] = 'Missing bounds for this parameter.';
			continue;
		}
		if (
			typeof b.lo !== 'number' ||
			!isFinite(b.lo) ||
			typeof b.hi !== 'number' ||
			!isFinite(b.hi)
		) {
			boundsErrors[id] = 'Bounds must be finite numbers.';
			continue;
		}
		if (b.lo >= b.hi) {
			boundsErrors[id] = 'lo must be less than hi.';
		}
	}
	if (Object.keys(boundsErrors).length > 0) {
		errors.bounds = boundsErrors;
	}

	const metric = typeof input.metricSeriesId === 'string' ? input.metricSeriesId.trim() : '';
	if (metric.length === 0) {
		errors.metricSeriesId = 'Enter a metric series id.';
	}

	if (
		typeof input.tolerance !== 'number' ||
		!isFinite(input.tolerance) ||
		input.tolerance <= 0
	) {
		errors.tolerance = 'tolerance must be a positive number.';
	}

	if (
		typeof input.maxIterations !== 'number' ||
		!isFinite(input.maxIterations) ||
		!Number.isInteger(input.maxIterations)
	) {
		errors.maxIterations = 'maxIterations must be an integer.';
	} else if (input.maxIterations < 1) {
		errors.maxIterations = 'maxIterations must be at least 1.';
	}

	if (Object.keys(errors).length === 0) return { ok: true };
	return { ok: false, errors };
}
