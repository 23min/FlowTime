// Pure formatting helpers for the What-If page.
//
// Extracted so they can be unit-tested without mounting the Svelte component.

/**
 * Format a number for compact display: integer as-is, float to 2 decimals.
 */
export function formatValue(v: number): string {
	if (Number.isInteger(v)) return v.toString();
	return v.toFixed(2);
}

/**
 * Format an array of numbers as a compact bracketed list.
 */
export function formatSeries(values: number[]): string {
	return '[' + values.map(formatValue).join(', ') + ']';
}

/**
 * Return true if a series name is a compiler-generated temporary or an
 * internal edge column that should never appear in the UI.
 *
 * Per-class columns (`{node}__class_{class}`) are NOT considered internal —
 * they are first-class user-visible series in class-based models.
 */
export function isCompilerTemp(name: string): boolean {
	return name.startsWith('__temp_') || name.startsWith('__edge_');
}

/**
 * Return true if a series name represents a per-class decomposition column
 * (`{node}__class_{classId}`). These are user-visible but often shown as
 * a secondary/grouped view alongside the parent series.
 */
export function isPerClassSeries(name: string): boolean {
	return name.includes('__class_');
}

/**
 * Return true if a series is "internal" in the strict sense — should never
 * appear in UI listings at all. Currently equivalent to `isCompilerTemp`.
 *
 * @deprecated Previously filtered out per-class series too. Use `isCompilerTemp`
 * for the strict filter or a page-specific filter that combines predicates.
 */
export function isInternalSeries(name: string): boolean {
	return isCompilerTemp(name);
}

/**
 * Parse a per-class series name into its parts.
 * Returns null if the name is not a per-class series.
 *
 * Examples:
 *   "arrivals__class_Order" → { base: "arrivals", classId: "Order" }
 *   "served__class_Refund" → { base: "served", classId: "Refund" }
 */
export function parseClassSeries(name: string): { base: string; classId: string } | null {
	const idx = name.indexOf('__class_');
	if (idx < 0) return null;
	const base = name.substring(0, idx);
	const classId = name.substring(idx + '__class_'.length);
	if (!base || !classId) return null;
	return { base, classId };
}
