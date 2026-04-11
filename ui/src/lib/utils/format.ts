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
 * Return true if a series name is internal (temp or per-class column)
 * and should be hidden from UI listings.
 */
export function isInternalSeries(name: string): boolean {
	return name.startsWith('__') || name.includes('__class_');
}
