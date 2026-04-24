/**
 * Normalize a raw metric value into `[0, 1]` given a shared `[lo, hi]` domain.
 *
 * Used by both the topology metric mapper (feeds the dag-map color palette) and
 * the heatmap grid to keep color interpretations consistent across views per
 * AC5 + ADR-m-E21-06-02.
 *
 * Returns:
 *   - a clamped linear position in `[0, 1]` when `lo < hi` and the value is finite.
 *   - `0.5` for the single-value `lo === hi` case so the mid-shade renders.
 *   - `null` for invalid input (non-finite value, inverted domain, non-finite domain bound).
 *
 * Callers treat `null` as "no color" — the node / cell renders as its baseline class color
 * rather than a metric-driven color.
 */
export function normalizeValueInDomain(
	value: number | null | undefined,
	domain: readonly [number, number]
): number | null {
	if (value === null || value === undefined) return null;
	if (!Number.isFinite(value)) return null;
	const [lo, hi] = domain;
	if (!Number.isFinite(lo) || !Number.isFinite(hi)) return null;
	if (lo > hi) return null;
	if (lo === hi) return 0.5;
	if (value <= lo) return 0;
	if (value >= hi) return 1;
	return (value - lo) / (hi - lo);
}
