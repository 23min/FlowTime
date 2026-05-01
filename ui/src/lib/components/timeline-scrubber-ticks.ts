/**
 * Pure logic for TimelineScrubber tick generation.
 * Extracted for unit-testing without DOM.
 */

export interface TickMark {
	bin: number;
	pct: number;
	label: string;
}

export interface TickResult {
	major: TickMark[];
	minor: number[];
}

/**
 * Compute major and minor tick marks for a scrubber of given bin count.
 * - Major ticks: up to ~10 evenly-spaced labeled positions, always including bin 0 and bin (binCount-1)
 * - Minor ticks: midpoints between adjacent major ticks, when midpoint is a distinct bin
 *
 * Returns empty arrays when binCount <= 1 (no scrubbing possible).
 */
export function computeTicks(binCount: number): TickResult {
	if (binCount <= 1) return { major: [], minor: [] };

	const targetLabels = Math.min(10, Math.max(2, Math.floor(binCount / 5)));
	const step = Math.max(1, Math.round((binCount - 1) / (targetLabels - 1)));
	const major: TickMark[] = [];

	for (let bin = 0; bin < binCount; bin += step) {
		major.push({
			bin,
			pct: (bin / (binCount - 1)) * 100,
			label: String(bin),
		});
	}
	const last = binCount - 1;
	if (major.length === 0 || major[major.length - 1].bin !== last) {
		major.push({ bin: last, pct: 100, label: String(last) });
	}

	const minor: number[] = [];
	for (let i = 0; i < major.length - 1; i++) {
		const midBin = Math.round((major[i].bin + major[i + 1].bin) / 2);
		if (midBin !== major[i].bin && midBin !== major[i + 1].bin) {
			minor.push((midBin / (binCount - 1)) * 100);
		}
	}

	return { major, minor };
}

/**
 * Compute the pointer position (percentage, 0-100) for a given current bin.
 * Returns 0 when binCount <= 1 (no meaningful position).
 */
export function computePointerPct(binCount: number, currentBin: number): number {
	if (binCount <= 1) return 0;
	return (currentBin / (binCount - 1)) * 100;
}
