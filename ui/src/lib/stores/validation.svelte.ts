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
 * Edge id format note: `edgeWarnings` keys are opaque strings the analyser
 * persisted in `RunWarning.EdgeIds` (see `BuildEdgeWarnings` at
 * `src/FlowTime.API/Services/StateQueryService.cs:4783`). The store treats
 * them as opaque — they may or may not match the workbench's `from→to`
 * convention. The panel's edge-pin click path is responsible for parsing
 * the key (or no-op'ing if it can't); the store does not interpret it.
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
 * Pure derivation — takes the raw `StateWindowResponse` (or null when no
 * run is loaded) and produces the full set of fields the rune wrapper
 * exposes. Exported for direct vitest coverage; the rune store calls this
 * exactly once per `setResponse(...)`.
 */
export function deriveValidationData(
	response: StateWindowResponse | null,
): DerivedValidation {
	if (response === null) {
		return EMPTY_DERIVATION;
	}

	const warnings = response.warnings;
	const edgeWarnings = response.edgeWarnings;

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

	get state(): 'empty' | 'issues' {
		return deriveValidationData(this.#response).state;
	}

	get rows(): ValidationRow[] {
		return deriveValidationData(this.#response).rows;
	}

	get nodeSeverityById(): Record<string, 'error' | 'warning' | 'info'> {
		return deriveValidationData(this.#response).nodeSeverityById;
	}

	get edgeSeverityById(): Record<string, 'error' | 'warning' | 'info'> {
		return deriveValidationData(this.#response).edgeSeverityById;
	}

	setResponse(response: StateWindowResponse | null): void {
		this.#response = response;
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
