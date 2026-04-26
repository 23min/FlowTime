/**
 * Time formatting for heatmap bin axis + cell tooltips (m-E21-06 AC3 + AC11).
 *
 * Three layers:
 *   - `formatBinAbsolute(isoUtc)`: UTC HH:MM from an ISO string.
 *   - `formatBinOffset(bin, binSize, binUnit)`: `±HH:MM` relative to bin 0.
 *   - `formatBinTime(bin, timestamps, grid)`: composite — absolute when
 *     `timestamps[bin]` is present, offset otherwise.
 *
 * Pure by design; the Svelte component delegates its formatting to these helpers.
 * Uses invariant formatting (UTC + two-digit padding) per repo convention.
 */

import type { BinUnit } from '$lib/utils/bin-label-stride.js';

export interface HeatmapTimeGrid {
	binSize: number;
	binUnit: string;
}

export function formatBinAbsolute(isoUtc: string): string {
	const d = new Date(isoUtc);
	if (isNaN(d.getTime())) return isoUtc;
	const hh = String(d.getUTCHours()).padStart(2, '0');
	const mm = String(d.getUTCMinutes()).padStart(2, '0');
	return `${hh}:${mm}`;
}

export function formatBinOffset(
	bin: number,
	binSize: number,
	binUnit: BinUnit | string
): string {
	let totalMinutes: number;
	switch (binUnit) {
		case 'seconds':
			totalMinutes = (bin * binSize) / 60;
			break;
		case 'hours':
			totalMinutes = bin * binSize * 60;
			break;
		case 'days':
			totalMinutes = bin * binSize * 60 * 24;
			break;
		default:
			// 'minutes' or unknown unit — treat as minutes.
			totalMinutes = bin * binSize;
	}
	const sign = totalMinutes < 0 ? '-' : '+';
	const abs = Math.abs(totalMinutes);
	const hh = Math.floor(abs / 60);
	const mm = Math.floor(abs % 60);
	return `${sign}${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}`;
}

export function formatBinTime(
	bin: number,
	timestampsUtc: ReadonlyArray<string> | undefined,
	grid: HeatmapTimeGrid | undefined
): string {
	if (timestampsUtc && timestampsUtc[bin]) {
		return formatBinAbsolute(timestampsUtc[bin]);
	}
	const binSize = grid?.binSize ?? 1;
	const binUnit = grid?.binUnit ?? 'minutes';
	return formatBinOffset(bin, binSize, binUnit);
}
