/**
 * Heatmap row-sort comparators — m-E21-06 AC6.
 *
 * Sort modes shipped in m-E21-06:
 *   - 'topological' (default) — graph walk; tie-break by node id.
 *   - 'id'                   — alphabetical by node id.
 *   - 'max'                  — descending by per-row max finite value; id tie-break.
 *   - 'mean'                 — descending by per-row arithmetic mean over finite cells; id tie-break.
 *   - 'variance'             — descending by per-row variance over finite cells; id tie-break.
 *
 * Sort is pin-agnostic: pinned rows sort in their natural position within the active
 * mode — they are NOT floated to the top. The pin glyph in the row-label gutter
 * (AC10) is the sole pinned-row indicator.
 *
 * Current-bin-value and trend/slope sort modes are deferred (tracked in work/gaps.md).
 */

export type SortMode = 'topological' | 'id' | 'max' | 'mean' | 'variance';

export interface HeatmapRowInput {
	/** Node id. Used as the stable tie-break key. */
	id: string;
	/**
	 * Full-window series for this row under the current metric. Nulls / undefined /
	 * NaN / Infinity are treated as missing and excluded from aggregations.
	 */
	series: ReadonlyArray<number | null | undefined>;
}

export interface SortEdge {
	from: string;
	to: string;
}

export interface SortOptions {
	mode: SortMode;
	/** Graph edges, used to derive a topological ordering when `mode === 'topological'`. */
	edges?: ReadonlyArray<SortEdge>;
	/**
	 * Pre-computed topological ordering of node ids. Providing this avoids re-running Kahn's
	 * algorithm inside the comparator on every sort. When both `edges` and `topoOrder` are
	 * supplied, `topoOrder` wins.
	 */
	topoOrder?: ReadonlyArray<string>;
}

/**
 * Produce a topological ordering of `nodeIds` given `edges`. Uses Kahn's algorithm with
 * node-id alphabetical tie-break among ready nodes to keep the output deterministic.
 *
 * Edges whose endpoints are not in `nodeIds` are ignored silently. If a cycle prevents
 * Kahn's algorithm from consuming the full graph, the remaining nodes are appended in
 * alphabetical order — the caller still gets a usable ordering even if a model pathology
 * arrives.
 */
export function topologicalOrder(
	nodeIds: ReadonlyArray<string>,
	edges: ReadonlyArray<SortEdge>
): string[] {
	if (nodeIds.length === 0) return [];
	const idSet = new Set(nodeIds);
	const inDegree = new Map<string, number>();
	const children = new Map<string, string[]>();
	for (const id of nodeIds) {
		inDegree.set(id, 0);
		children.set(id, []);
	}
	for (const e of edges) {
		if (!idSet.has(e.from) || !idSet.has(e.to)) continue;
		inDegree.set(e.to, (inDegree.get(e.to) ?? 0) + 1);
		children.get(e.from)!.push(e.to);
	}

	// Priority queue via simple sort-on-insert: small graphs make an array fine.
	const ready: string[] = [];
	for (const [id, deg] of inDegree) if (deg === 0) ready.push(id);
	ready.sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));

	const result: string[] = [];
	while (ready.length > 0) {
		const next = ready.shift()!;
		result.push(next);
		for (const child of children.get(next) ?? []) {
			const d = (inDegree.get(child) ?? 0) - 1;
			inDegree.set(child, d);
			if (d === 0) {
				// Insert in alpha position to maintain deterministic tie-break.
				const insertAt = binarySearchInsertIndex(ready, child);
				ready.splice(insertAt, 0, child);
			}
		}
	}

	if (result.length < nodeIds.length) {
		// Cycle remnant: append whatever's left in alpha order.
		const emitted = new Set(result);
		const leftovers = nodeIds.filter((id) => !emitted.has(id)).slice();
		leftovers.sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
		result.push(...leftovers);
	}
	return result;
}

function binarySearchInsertIndex(sorted: string[], value: string): number {
	let lo = 0;
	let hi = sorted.length;
	while (lo < hi) {
		const mid = (lo + hi) >>> 1;
		if (sorted[mid] < value) lo = mid + 1;
		else hi = mid;
	}
	return lo;
}

function idCompare(a: string, b: string): number {
	if (a < b) return -1;
	if (a > b) return 1;
	return 0;
}

/** Per-row maximum over finite cells. Returns `-Infinity` when no finite cells are present
 *  so rows with no data sink to the bottom. */
function rowMax(r: HeatmapRowInput): number {
	let m = -Infinity;
	for (const v of r.series) {
		if (v === null || v === undefined) continue;
		if (Number.isFinite(v)) {
			if (v > m) m = v;
		}
	}
	return m;
}

/** Per-row arithmetic mean over finite cells. Returns `-Infinity` when no finite cells
 *  are present so rows with no data sink to the bottom. */
function rowMean(r: HeatmapRowInput): number {
	let sum = 0;
	let n = 0;
	for (const v of r.series) {
		if (v === null || v === undefined) continue;
		if (Number.isFinite(v)) {
			sum += v;
			n += 1;
		}
	}
	if (n === 0) return -Infinity;
	return sum / n;
}

/** Per-row variance (population variance over finite cells). Returns 0 when fewer than
 *  two finite cells are present; sinks those rows via id tie-break. */
function rowVariance(r: HeatmapRowInput): number {
	let sum = 0;
	let n = 0;
	for (const v of r.series) {
		if (v === null || v === undefined) continue;
		if (Number.isFinite(v)) {
			sum += v;
			n += 1;
		}
	}
	if (n < 2) return 0;
	const mean = sum / n;
	let sqSum = 0;
	for (const v of r.series) {
		if (v === null || v === undefined) continue;
		if (Number.isFinite(v)) {
			const d = v - mean;
			sqSum += d * d;
		}
	}
	return sqSum / n;
}

/**
 * Sort heatmap rows into render order.
 *
 * Returns a new array; input is not mutated. Sort is pin-agnostic — rows sort purely
 * by the active mode. Pinned rows keep their natural sort position; the pin glyph
 * in the row-label gutter (AC10) is the sole pinned-row indicator.
 */
export function sortHeatmapRows(
	rows: ReadonlyArray<HeatmapRowInput>,
	options: SortOptions
): HeatmapRowInput[] {
	if (rows.length === 0) return [];
	const cmp = buildComparator(rows, options);
	return [...rows].sort(cmp);
}

function buildComparator(
	rows: ReadonlyArray<HeatmapRowInput>,
	options: SortOptions
): (a: HeatmapRowInput, b: HeatmapRowInput) => number {
	switch (options.mode) {
		case 'id':
			return (a, b) => idCompare(a.id, b.id);

		case 'max': {
			const cache = new Map<string, number>();
			const get = (r: HeatmapRowInput) => {
				let v = cache.get(r.id);
				if (v === undefined) {
					v = rowMax(r);
					cache.set(r.id, v);
				}
				return v;
			};
			return (a, b) => {
				const va = get(a);
				const vb = get(b);
				if (va !== vb) return vb - va; // desc
				return idCompare(a.id, b.id);
			};
		}

		case 'mean': {
			const cache = new Map<string, number>();
			const get = (r: HeatmapRowInput) => {
				let v = cache.get(r.id);
				if (v === undefined) {
					v = rowMean(r);
					cache.set(r.id, v);
				}
				return v;
			};
			return (a, b) => {
				const va = get(a);
				const vb = get(b);
				if (va !== vb) return vb - va; // desc
				return idCompare(a.id, b.id);
			};
		}

		case 'variance': {
			const cache = new Map<string, number>();
			const get = (r: HeatmapRowInput) => {
				let v = cache.get(r.id);
				if (v === undefined) {
					v = rowVariance(r);
					cache.set(r.id, v);
				}
				return v;
			};
			return (a, b) => {
				const va = get(a);
				const vb = get(b);
				if (va !== vb) return vb - va; // desc
				return idCompare(a.id, b.id);
			};
		}

		case 'topological': {
			const order = resolveTopologicalOrder(rows, options);
			const rank = new Map<string, number>();
			for (let i = 0; i < order.length; i++) rank.set(order[i], i);
			const LAST = Number.MAX_SAFE_INTEGER;
			return (a, b) => {
				const ra = rank.get(a.id) ?? LAST;
				const rb = rank.get(b.id) ?? LAST;
				if (ra !== rb) return ra - rb;
				return idCompare(a.id, b.id);
			};
		}

		default:
			// Defensive fallback — unknown mode behaves like 'id'.
			return (a, b) => idCompare(a.id, b.id);
	}
}

function resolveTopologicalOrder(
	rows: ReadonlyArray<HeatmapRowInput>,
	options: SortOptions
): ReadonlyArray<string> {
	if (options.topoOrder && options.topoOrder.length > 0) return options.topoOrder;
	const ids = rows.map((r) => r.id);
	return topologicalOrder(ids, options.edges ?? []);
}
