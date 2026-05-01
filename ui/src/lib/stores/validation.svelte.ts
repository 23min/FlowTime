/**
 * Validation store — m-E21-07 AC11.
 *
 * Single source of truth for the validation rows + per-node + per-edge
 * severity-max maps derived from a `state_window` response. The store does
 * NOT fetch — the route owns the fetch (existing `loadWindow()` in
 * `+page.svelte`) and pushes the response into the store after each
 * successful load. The panel component, the topology node indicators (AC7),
 * and the topology edge indicators (AC8) all read from this single store.
 *
 * The pure `deriveValidationData(response | null)` function is the seam under
 * test: it is exported for direct vitest coverage. The rune-backed
 * `ValidationStore` wraps it and re-derives whenever `setResponse(...)`
 * mutates the held response.
 *
 * Store lift rationale: the route's `loadWindow()` already deserializes the
 * full `StateWindowResponse` but only reaches into `nodes` and
 * `timestampsUtc`. Routing the same response into this store (one extra
 * `validation.setResponse(value)` call after `windowNodes = ...`) is a
 * minimal lift — no duplicate fetch, no shape change, no extra round-trip.
 *
 * Edge id format note (smoke-test fix 2026-04-27): `edgeWarnings` keys arrive
 * keyed by the analyser's persisted edge id (`RunWarning.EdgeIds` — see
 * `BuildEdgeWarnings` at `src/FlowTime.API/Services/StateQueryService.cs`).
 * For lag YAML that's the YAML's `edges[].id` field (e.g. `"source_to_target"`),
 * NOT the workbench's `from→to` convention the topology / panel / workbench-
 * edge-card all rely on. To unify the three surfaces, `setResponse(...)` now
 * accepts an optional second arg carrying the graph's edge metadata (`id`,
 * `from`, `to`); when provided, the store rewrites raw `edgeWarnings` keys to
 * the workbench `${from}→${to}` form before deriving rows + the severity-max
 * map. Downstream consumers (panel, topology indicators, edge-card lookup)
 * see consistent keys and do not need their own translation logic.
 *
 * When `edges` is omitted (mocked tests, legacy callers), keys are preserved
 * raw — the translation is a one-way enrichment, not a behaviour change for
 * call sites that haven't opted in.
 */

import { sortValidationItems, type ValidationRow } from '$lib/utils/validation-helpers.js';
import type { StateWarning, StateWindowResponse } from '$lib/api/types.js';

/**
 * Severity rank — kept private to the module so the public store surface
 * never leaks internal numbers. Mirrors the rank in `validation-helpers.ts`
 * (the helper there is internal too); duplicating here is cheaper than
 * exporting and binding the two modules. If the rank semantics ever drift,
 * the store + helper test suites both fail.
 */
const SEVERITY_RANK: Record<string, number> = {
	error: 3,
	warning: 2,
	info: 1,
};

function severityRank(severity: string): number {
	return SEVERITY_RANK[severity] ?? 0;
}

/**
 * The shape produced by `deriveValidationData(...)`. Fields:
 *
 * - `state` — `'empty' | 'issues'`. Same semantics as
 *   `classifyValidationState` from `validation-helpers.ts`. The store
 *   exposes this so the panel component can collapse the column without
 *   importing the classifier separately.
 * - `rows` — flat sorted list ready for direct `{#each}` consumption.
 * - `nodeSeverityById` / `edgeSeverityById` — severity-max maps; only keys
 *   with at least one *known* severity (`error` / `warning` / `info`)
 *   appear. Used by the topology AC7 / AC8 indicators in the next chunk.
 */
export interface DerivedValidation {
	state: 'empty' | 'issues';
	rows: ValidationRow[];
	nodeSeverityById: Record<string, 'error' | 'warning' | 'info'>;
	edgeSeverityById: Record<string, 'error' | 'warning' | 'info'>;
}

const EMPTY_DERIVATION: DerivedValidation = Object.freeze({
	state: 'empty' as const,
	rows: Object.freeze([]) as unknown as ValidationRow[],
	nodeSeverityById: Object.freeze({}) as Record<string, 'error' | 'warning' | 'info'>,
	edgeSeverityById: Object.freeze({}) as Record<string, 'error' | 'warning' | 'info'>,
});

/**
 * Build a node-kind row from a raw `StateWarning`. The `key` is the warning's
 * `nodeId` when present; otherwise `null` (signals "no clickable identity"
 * to the panel per AC9 / AC2's "no pin affordance" branch).
 */
function buildNodeRow(warning: StateWarning): ValidationRow {
	return {
		kind: 'node',
		key: warning.nodeId ?? null,
		severity: warning.severity,
		message: warning.message,
		code: warning.code,
		startBin: warning.startBin,
		endBin: warning.endBin,
		signal: warning.signal,
	};
}

/**
 * Build an edge-kind row. The `key` is the edge id (the map key in
 * `edgeWarnings`); the source warning's own `nodeId` is intentionally
 * dropped — edge identity comes from the map key, not the warning body.
 */
function buildEdgeRow(edgeId: string, warning: StateWarning): ValidationRow {
	return {
		kind: 'edge',
		key: edgeId,
		severity: warning.severity,
		message: warning.message,
		code: warning.code,
		startBin: warning.startBin,
		endBin: warning.endBin,
		signal: warning.signal,
	};
}

/**
 * Mutating helper — promote `current` to the most-severe of the two
 * severities. Returns the new winner, or `null` if neither is a known
 * literal (the empty-known-bucket case the test suite covers).
 */
function promoteSeverity(
	current: 'error' | 'warning' | 'info' | null,
	next: string,
): 'error' | 'warning' | 'info' | null {
	const nextRank = severityRank(next);
	if (nextRank === 0) return current; // unknown literal — never promotes
	const currentRank = current === null ? 0 : severityRank(current);
	if (nextRank > currentRank) {
		return next as 'error' | 'warning' | 'info';
	}
	return current;
}

/**
 * Edge metadata accepted by `setResponse(...)` / `deriveValidationData(...)`
 * for raw-key → `from→to` translation. Shape mirrors `GraphResponse.edges[]`
 * — only `id`, `from`, `to` are read; extra fields ride along untouched.
 */
export interface EdgeMetadata {
	id?: string;
	from: string;
	to: string;
}

/**
 * Translate a raw `edgeWarnings` key into the workbench's `${from}→${to}`
 * convention if a matching graph edge exists. Returns the raw key as a
 * graceful fallback when no mapping is found — downstream consumers handle
 * the unmapped case the same way they handled all opaque keys before.
 *
 * One `console.warn` is emitted per unmapped key per call so smoke-test
 * regressions surface in the dev console without spamming the log.
 */
function translateEdgeKey(
	rawKey: string,
	edgeMap: Map<string, string> | null,
): string {
	if (edgeMap === null) return rawKey;
	const translated = edgeMap.get(rawKey);
	if (translated !== undefined) return translated;
	// Already in `from→to` form? The map keys it both by analyser id AND by
	// the from→to string itself, so we reach this branch only when the key
	// genuinely does not match any known edge. Log once per pass per key —
	// debug aid for fixture / analyser drift.
	console.warn('validation: unmapped edge key (no graph edge matched)', { rawKey });
	return rawKey;
}

/**
 * Build the `analyserId → from→to` map used for translation. The map keys
 * include both `edge.id` (when present) AND `${edge.from}→${edge.to}`
 * itself, so callers that already pass `from→to` keys (mocked specs,
 * tests) round-trip through the translator unchanged.
 *
 * Returns `null` when `edges` is null/undefined/empty — null short-circuits
 * the translator to identity behaviour (back-compat with callers that don't
 * pass graph metadata).
 */
/**
 * Strip a `:port` suffix from a graph edge endpoint. The graph response
 * carries port-suffixed identifiers (`"SourceNode:out"`, `"TargetNode:in"`)
 * but the dag-map renders `data-edge-from="SourceNode"` (port stripped) — the
 * topology indicator selector and the workbench-edge-card lookup both use the
 * stripped form. Mirrors `StateQueryService.ExtractNodeReference` on the
 * backend.
 */
function stripPort(reference: string): string {
	const idx = reference.indexOf(':');
	return idx < 0 ? reference : reference.slice(0, idx);
}

function buildEdgeKeyMap(
	edges: EdgeMetadata[] | null | undefined,
): Map<string, string> | null {
	if (!edges || edges.length === 0) return null;
	const map = new Map<string, string>();
	for (const edge of edges) {
		const target = `${stripPort(edge.from)}→${stripPort(edge.to)}`;
		if (edge.id) map.set(edge.id, target);
		// Also map the raw (port-suffixed) from→to → stripped target so callers
		// that pre-translate without stripping ports also resolve correctly.
		const rawTarget = `${edge.from}→${edge.to}`;
		if (rawTarget !== target) map.set(rawTarget, target);
		// Map stripped from→to → itself so mocked specs that already emit
		// stripped keys round-trip through the translator unchanged.
		map.set(target, target);
	}
	return map;
}

/**
 * Pure derivation — takes the raw `StateWindowResponse` (or null when no
 * run is loaded) and produces the full set of fields the rune wrapper
 * exposes. Exported for direct vitest coverage; the rune store calls this
 * exactly once per `setResponse(...)`.
 *
 * Optional `edges` — graph metadata enabling the wire-to-UI edge-key
 * translation (smoke-test fix 2026-04-27). When supplied, raw `edgeWarnings`
 * keys (analyser ids like `source_to_target`) are rewritten to the workbench
 * `${from}→${to}` convention. When omitted, keys are preserved raw so legacy
 * tests and mocked specs see no change.
 */
export function deriveValidationData(
	response: StateWindowResponse | null,
	edges?: EdgeMetadata[] | null,
): DerivedValidation {
	if (response === null) {
		return EMPTY_DERIVATION;
	}

	const warnings = response.warnings;
	const rawEdgeWarnings = response.edgeWarnings;
	const edgeMap = buildEdgeKeyMap(edges);

	// Translate the raw edgeWarnings keys to the workbench `from→to` form when
	// the graph metadata is available. Re-aggregate per translated key — two
	// raw keys could in principle collide on the same translated key (would
	// require two graph edges sharing the same from/to which is degenerate,
	// but the merge path is robust regardless).
	const edgeWarnings: Record<string, StateWarning[]> =
		edgeMap === null ? rawEdgeWarnings : (() => {
			const out: Record<string, StateWarning[]> = {};
			for (const rawKey of Object.keys(rawEdgeWarnings)) {
				const items = rawEdgeWarnings[rawKey];
				const translatedKey = translateEdgeKey(rawKey, edgeMap);
				if (out[translatedKey]) {
					out[translatedKey] = out[translatedKey].concat(items ?? []);
				} else {
					out[translatedKey] = items ?? [];
				}
			}
			return out;
		})();

	// Build the flat row list — node rows first, then edge rows. The sort
	// step rearranges them; building in order keeps the function easy to
	// reason about.
	const rows: ValidationRow[] = [];
	for (const warning of warnings) {
		rows.push(buildNodeRow(warning));
	}
	for (const edgeId of Object.keys(edgeWarnings)) {
		const items = edgeWarnings[edgeId];
		// Skip edges with empty arrays — the panel-state classifier treats
		// key-presence as "issues" but the row list needs at least one warning
		// per edge to render a row.
		if (!items || items.length === 0) continue;
		for (const warning of items) {
			rows.push(buildEdgeRow(edgeId, warning));
		}
	}

	// Severity-max maps — built from the same source as the rows so the two
	// surfaces cannot disagree. Only known severities populate the map; an
	// edge or node whose warnings are all unknown literals is omitted.
	const nodeSeverityById: Record<string, 'error' | 'warning' | 'info'> = {};
	for (const warning of warnings) {
		if (!warning.nodeId) continue;
		const promoted = promoteSeverity(
			nodeSeverityById[warning.nodeId] ?? null,
			warning.severity,
		);
		if (promoted !== null) {
			nodeSeverityById[warning.nodeId] = promoted;
		}
	}

	const edgeSeverityById: Record<string, 'error' | 'warning' | 'info'> = {};
	for (const edgeId of Object.keys(edgeWarnings)) {
		const items = edgeWarnings[edgeId];
		if (!items) continue;
		for (const warning of items) {
			const promoted = promoteSeverity(
				edgeSeverityById[edgeId] ?? null,
				warning.severity,
			);
			if (promoted !== null) {
				edgeSeverityById[edgeId] = promoted;
			}
		}
	}

	const state: 'empty' | 'issues' =
		rows.length === 0 && Object.keys(edgeWarnings).length === 0 ? 'empty' : 'issues';

	return {
		state,
		rows: sortValidationItems(rows),
		nodeSeverityById,
		edgeSeverityById,
	};
}

/**
 * Rune-backed wrapper. Holds the latest `StateWindowResponse` (or null)
 * and re-derives the four published fields whenever `setResponse(...)` is
 * called. Routes / components import the module-level `validation` instance
 * directly; tests construct their own `ValidationStore` to assert
 * derivation behaviour without leaking state across describe-blocks.
 */
export class ValidationStore {
	#response = $state<StateWindowResponse | null>(null);
	#edges = $state<EdgeMetadata[] | null>(null);

	get state(): 'empty' | 'issues' {
		return deriveValidationData(this.#response, this.#edges).state;
	}

	get rows(): ValidationRow[] {
		return deriveValidationData(this.#response, this.#edges).rows;
	}

	get nodeSeverityById(): Record<string, 'error' | 'warning' | 'info'> {
		return deriveValidationData(this.#response, this.#edges).nodeSeverityById;
	}

	get edgeSeverityById(): Record<string, 'error' | 'warning' | 'info'> {
		return deriveValidationData(this.#response, this.#edges).edgeSeverityById;
	}

	/**
	 * Push a fresh response into the store. Optional `edges` carries the graph
	 * metadata for raw-key → `from→to` translation (smoke-test fix 2026-04-27).
	 * Pass `graph.edges` from `GET /v1/runs/{id}/graph`; omit when the caller
	 * has no graph metadata (mocked tests, legacy paths) — keys are preserved
	 * raw and existing assertions remain intact.
	 */
	setResponse(
		response: StateWindowResponse | null,
		edges?: EdgeMetadata[] | null,
	): void {
		this.#response = response;
		this.#edges = edges ?? null;
	}
}

/**
 * Default module-level instance — wired into the topology route via a single
 * `validation.setResponse(value)` call after `loadWindow()` succeeds. The
 * panel component reads this instance directly; topology AC7 / AC8
 * indicators in the next chunk consume `nodeSeverityById` /
 * `edgeSeverityById` from it.
 */
export const validation = new ValidationStore();
