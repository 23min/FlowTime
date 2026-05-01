/**
 * Pure helpers for the analysis surfaces (sweep, sensitivity, etc.).
 *
 * Separated from Svelte components so every branch can be unit-tested
 * without a DOM or reactive runtime.
 */

import yaml from 'js-yaml';

export interface ConstParam {
	id: string;
	baseline: number;
	/** Full values array from the YAML (per-bin). */
	values: number[];
}

/**
 * Parse a model YAML string and extract all const-kind nodes as sweepable parameters.
 * Returns [] on parse error or when no const nodes exist.
 *
 * A parameter's baseline is the first value in its `values` array;
 * parameters with empty values or non-numeric first entries are skipped.
 */
export function discoverConstParams(modelYaml: string): ConstParam[] {
	if (!modelYaml || typeof modelYaml !== 'string') return [];
	let doc: unknown;
	try {
		doc = yaml.load(modelYaml);
	} catch {
		return [];
	}
	if (!doc || typeof doc !== 'object') return [];
	const nodes = (doc as Record<string, unknown>).nodes;
	if (!Array.isArray(nodes)) return [];

	const params: ConstParam[] = [];
	for (const raw of nodes) {
		if (!raw || typeof raw !== 'object') continue;
		const node = raw as Record<string, unknown>;
		if (node.kind !== 'const') continue;
		const id = typeof node.id === 'string' ? node.id : undefined;
		if (!id) continue;
		const values = node.values;
		if (!Array.isArray(values) || values.length === 0) continue;
		const numericValues = values.map((v) => Number(v));
		const baseline = numericValues[0];
		if (!isFinite(baseline)) continue;
		params.push({ id, baseline, values: numericValues });
	}
	return params;
}

export interface GeneratedRange {
	values: number[];
	truncated: boolean;
	requestedCount: number;
}

/**
 * Generate a linear sweep value range with truncation metadata.
 *
 * `values` is [] when from/to/step produce no valid range or when step is
 * non-positive. The output is capped at `maxPoints` (default 200) to protect
 * the API; when that cap fires, `truncated` is true and `requestedCount`
 * reports the full range the user asked for so the UI can surface a distinct
 * "clipped to N of M" signal instead of a generic "too many points" warning.
 */
export function generateRange(
	from: number,
	to: number,
	step: number,
	maxPoints = 200
): GeneratedRange {
	if (!isFinite(from) || !isFinite(to) || !isFinite(step) || step <= 0 || to < from) {
		return { values: [], truncated: false, requestedCount: 0 };
	}

	const requestedCount = Math.floor((to - from) / step + 1e-9) + 1;

	const values: number[] = [];
	// Use epsilon-tolerant loop to avoid floating-point overshoot
	const eps = step * 1e-9;
	for (let v = from; v <= to + eps && values.length < maxPoints; v += step) {
		// Round to the nearest grid point to avoid float drift
		const rounded = Math.round(v / step) * step;
		// If step is sub-1, preserve up to ~12 decimal places
		const fixed = Number(rounded.toPrecision(12));
		values.push(fixed);
	}

	return {
		values,
		truncated: values.length === maxPoints && requestedCount > maxPoints,
		requestedCount,
	};
}

/**
 * Parse a comma-separated custom values string into a numeric array.
 * Whitespace is tolerated; non-numeric entries are dropped.
 * Returns [] when nothing parses.
 */
export function parseCustomValues(input: string): number[] {
	if (!input || typeof input !== 'string') return [];
	return input
		.split(',')
		.map((s) => s.trim())
		.filter((s) => s.length > 0)
		.map((s) => Number(s))
		.filter((n) => isFinite(n));
}

/**
 * Extract topology node ids from a model YAML.
 * Returns [] on parse error or when no topology.nodes array is present.
 */
export function discoverTopologyNodeIds(modelYaml: string): string[] {
	if (!modelYaml || typeof modelYaml !== 'string') return [];
	let doc: unknown;
	try {
		doc = yaml.load(modelYaml);
	} catch {
		return [];
	}
	if (!doc || typeof doc !== 'object') return [];
	const topology = (doc as Record<string, unknown>).topology;
	if (!topology || typeof topology !== 'object') return [];
	const nodes = (topology as Record<string, unknown>).nodes;
	if (!Array.isArray(nodes)) return [];

	const ids: string[] = [];
	for (const raw of nodes) {
		if (!raw || typeof raw !== 'object') continue;
		const id = (raw as Record<string, unknown>).id;
		if (typeof id === 'string' && id.length > 0) ids.push(id);
	}
	return ids;
}

/**
 * Convert a node id to the snake_case form the Rust engine uses for derived
 * series (e.g. "Queue" → "queue", "HTTPService" → "http_service").
 * Mirrors engine/core/src/compiler.rs `to_snake_case`.
 */
export function toSnakeCase(id: string): string {
	if (!id) return '';
	let out = '';
	for (let i = 0; i < id.length; i++) {
		const c = id[i];
		if (c >= 'A' && c <= 'Z') {
			const prev = i > 0 ? id[i - 1] : '';
			const next = i < id.length - 1 ? id[i + 1] : '';
			const prevLower = prev >= 'a' && prev <= 'z';
			const nextLower = next >= 'a' && next <= 'z';
			if (i > 0 && (prevLower || nextLower)) out += '_';
			out += c.toLowerCase();
		} else {
			out += c;
		}
	}
	return out;
}

/**
 * Given topology node ids, return the queue-depth series ids the Rust engine
 * emits: `{snake_case_id}_queue`.
 */
export function queueSeriesIds(topologyNodeIds: string[]): string[] {
	return topologyNodeIds.map((id) => `${toSnakeCase(id)}_queue`);
}

/**
 * Compute the mean of a numeric array, ignoring non-finite values.
 * Returns NaN for an all-non-finite or empty input.
 */
export function seriesMean(values: number[]): number {
	if (!Array.isArray(values) || values.length === 0) return NaN;
	let sum = 0;
	let count = 0;
	for (const v of values) {
		if (isFinite(v)) {
			sum += v;
			count++;
		}
	}
	return count === 0 ? NaN : sum / count;
}

export interface SweepPoint {
	paramValue: number;
	series: Record<string, number[]>;
}

export interface SweepResponse {
	paramId: string;
	points: SweepPoint[];
}

/**
 * Project a sweep response into a per-series, per-point mean table.
 * Result shape: Map<seriesId, Array<{ paramValue, mean }>>.
 * Series ids come from the first point's series keys (preserves insertion order).
 */
export function projectSweepMeans(
	response: SweepResponse
): Map<string, { paramValue: number; mean: number }[]> {
	const out = new Map<string, { paramValue: number; mean: number }[]>();
	if (!response || !Array.isArray(response.points) || response.points.length === 0) return out;

	const firstKeys = Object.keys(response.points[0].series ?? {});
	for (const key of firstKeys) {
		const rows = response.points.map((pt) => ({
			paramValue: pt.paramValue,
			mean: seriesMean(pt.series[key] ?? []),
		}));
		out.set(key, rows);
	}
	return out;
}

export interface SensitivityPoint {
	paramId: string;
	baseValue: number;
	gradient: number;
}

export interface SensitivityResponse {
	metricSeriesId: string;
	points: SensitivityPoint[];
}

/**
 * Sort sensitivity points by |gradient| descending; non-finite gradients go last.
 * Returns a new array, does not mutate.
 */
export function sortByAbsGradient(points: SensitivityPoint[]): SensitivityPoint[] {
	return [...points].sort((a, b) => {
		const aFinite = isFinite(a.gradient);
		const bFinite = isFinite(b.gradient);
		if (aFinite && !bFinite) return -1;
		if (!aFinite && bFinite) return 1;
		if (!aFinite && !bFinite) return 0;
		return Math.abs(b.gradient) - Math.abs(a.gradient);
	});
}

/**
 * Given the list of sensitivity points, return the maximum absolute gradient
 * (used to scale the bar chart). Returns 0 when no finite gradients.
 */
export function maxAbsGradient(points: SensitivityPoint[]): number {
	let max = 0;
	for (const p of points) {
		if (isFinite(p.gradient) && Math.abs(p.gradient) > max) max = Math.abs(p.gradient);
	}
	return max;
}
