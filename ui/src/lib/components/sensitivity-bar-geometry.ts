/**
 * Pure geometry + formatting helpers for SensitivityBarChart.
 * Extracted for unit testing without a DOM.
 */

export interface BarGeometry {
	x: number;
	w: number;
	color: string;
}

export interface BarLayout {
	labelWidth: number;
	valueWidth: number;
	width: number;
}

export function defaultLayout(width: number): BarLayout {
	return { labelWidth: 120, valueWidth: 56, width };
}

export function barAreaWidth(layout: BarLayout): number {
	return Math.max(80, layout.width - layout.labelWidth - layout.valueWidth);
}

export function barCenter(layout: BarLayout): number {
	return layout.labelWidth + barAreaWidth(layout) / 2;
}

/**
 * Compute the x/width/color for a single bar given a gradient value and
 * the current max. `max` is the scaling reference (usually maxAbsGradient).
 */
export function barGeometry(
	gradient: number,
	max: number,
	layout: BarLayout
): BarGeometry {
	const center = barCenter(layout);
	if (!isFinite(gradient) || max === 0) {
		return { x: center, w: 0, color: 'var(--muted-foreground)' };
	}
	const area = barAreaWidth(layout);
	const frac = Math.abs(gradient) / max;
	const w = (area / 2) * frac;
	if (gradient < 0) return { x: center - w, w, color: 'var(--ft-viz-coral)' };
	return { x: center, w, color: 'var(--ft-viz-teal)' };
}

/** Format a number for display in the bar chart value column. */
export function fmtBarValue(v: number): string {
	if (!isFinite(v)) return '—';
	if (Math.abs(v) >= 100) return v.toFixed(0);
	if (Math.abs(v) >= 1) return v.toFixed(2);
	return v.toFixed(3);
}
