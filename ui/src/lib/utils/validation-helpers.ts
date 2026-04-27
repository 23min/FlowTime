/**
 * Pure helpers for the m-E21-07 Validation Surface.
 *
 * Separated from Svelte components so every branch can be unit-tested without
 * a DOM or reactive runtime. Consumed later by the workbench validation panel
 * and the topology node / edge warning indicators (ACs 2 / 3 / 7 / 8 / 9 / 10).
 *
 * All helpers in this module are pure and synchronous; no I/O, no mutation of
 * inputs, no time/clock dependencies.
 */

import type { StateWarning } from '../api/types.js';

/**
 * Classifier input: the two warning surfaces carried on a `state_window`
 * response (per the AC1 type widening). The classifier tells the panel
 * whether to render the empty state or the populated list.
 */
export interface ValidationStateInput {
	warnings: StateWarning[];
	edgeWarnings: Record<string, StateWarning[]>;
}

/**
 * Classify the run's validation surface as empty (collapse the panel) or
 * populated (render the list).
 *
 * - `empty` when `warnings.length === 0` AND `Object.keys(edgeWarnings).length === 0`.
 * - `issues` otherwise.
 *
 * Edge-case design choice: when `edgeWarnings` has a key whose value array
 * is empty (defensive shape that should never actually arrive from
 * `StateQueryService.BuildEdgeWarnings` — the backend only emits keys with
 * warnings), we still treat the run as `'issues'`. Key-presence is the
 * signal, not value-population. Matches the wire contract — the API emits a
 * key only when the analyser flagged that edge.
 */
export function classifyValidationState(input: ValidationStateInput): 'empty' | 'issues' {
	const hasNodeWarnings = input.warnings.length > 0;
	const hasEdgeKeys = Object.keys(input.edgeWarnings).length > 0;
	if (!hasNodeWarnings && !hasEdgeKeys) return 'empty';
	return 'issues';
}

/**
 * Row shape consumed by the validation panel. Each row carries the warning's
 * `kind` (`'node'` or `'edge'`) plus the keyed identity used for cross-link
 * and rendering: `nodeId` for node rows, `edgeId` for edge rows. `key` is
 * `null` for warnings the API returned without a `nodeId` (and therefore
 * non-clickable per AC9 / AC2's "no pin affordance" branch).
 *
 * The shape carries `code` and `message` from the wire DTO so the panel can
 * render the chip + copy without a second lookup. Additional fields from the
 * source `StateWarning` (`startBin`, `endBin`, `signal`) ride on the index
 * signature for downstream consumers; the sort never reads them.
 */
export interface ValidationRow {
	kind: 'node' | 'edge';
	key: string | null;
	severity: string;
	message: string;
	code: string;
	[extra: string]: unknown;
}

/**
 * Severity rank — higher number sorts earlier. Unknown literals collapse to
 * the same lowest rank so the sort is stable for them; the type widens to
 * accept whatever the backend emits today (`error`/`warning`/`info`).
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
 * Empty-or-null keys sort after non-empty keys. Treats `''` and `null`
 * identically — both signal "no keyed identity" to the panel.
 */
function isEmptyKey(key: string | null): boolean {
	return key === null || key === '';
}

/**
 * Sort a flat array of validation rows by:
 *   1. severity descending (`error` > `warning` > `info` > unknown)
 *   2. keyed identity ascending (`nodeId` for node rows, `edgeId` for edge
 *      rows; empty / null sorts last regardless of kind)
 *   3. message ascending
 *
 * Returns a new array; does not mutate the input.
 */
export function sortValidationItems(items: ValidationRow[]): ValidationRow[] {
	return [...items].sort((a, b) => {
		const sevDiff = severityRank(b.severity) - severityRank(a.severity);
		if (sevDiff !== 0) return sevDiff;

		const aEmpty = isEmptyKey(a.key);
		const bEmpty = isEmptyKey(b.key);
		if (aEmpty && !bEmpty) return 1;
		if (!aEmpty && bEmpty) return -1;
		if (!aEmpty && !bEmpty) {
			// Both non-empty — compare the strings directly.
			const ak = a.key as string;
			const bk = b.key as string;
			if (ak < bk) return -1;
			if (ak > bk) return 1;
		}
		// Both empty (null/'') — fall through to message tie-break.

		if (a.message < b.message) return -1;
		if (a.message > b.message) return 1;
		return 0;
	});
}

/**
 * Severity → chrome-token literal. Returns the CSS custom-property name
 * (without the `var(...)` wrapper) so consumers compose `style="color:
 * var(${token})"`.
 *
 * Returns `null` for unknown severities — consumers handle this with the
 * same `undefined`-fallback pattern m-E21-06 set for `--ft-pin` and
 * `--ft-highlight` (e.g. `style={token ? 'color: var(' + token + ')' :
 * undefined}`). This is the "no chrome treatment" sentinel.
 *
 * Per AC12 (m-E21-07): `--ft-info` ships in this milestone alongside
 * `--ft-warn` and `--ft-err`.
 */
export function severityChromeToken(
	severity: string,
): '--ft-warn' | '--ft-err' | '--ft-info' | null {
	switch (severity) {
		case 'error':
			return '--ft-err';
		case 'warning':
			return '--ft-warn';
		case 'info':
			return '--ft-info';
		default:
			return null;
	}
}

/**
 * Reduce a bag of warnings to the most-severe known severity. Used to drive
 * the per-node and per-edge severity-max indicator chrome (AC7 / AC8) — when
 * multiple warnings target the same node or edge, the most severe wins.
 *
 * - Returns `null` for an empty array OR when no warning carries a known
 *   severity. Unknown severities (literals outside `error`/`warning`/`info`)
 *   are ignored — they cannot promote past known literals.
 * - Reuses the same severity rank as `sortValidationItems`, so the two
 *   helpers cannot disagree.
 */
export function maxSeverityForKey(
	items: StateWarning[],
): 'error' | 'warning' | 'info' | null {
	let bestRank = 0;
	let bestSeverity: 'error' | 'warning' | 'info' | null = null;
	for (const item of items) {
		const rank = severityRank(item.severity);
		if (rank > bestRank) {
			bestRank = rank;
			// Narrow the wire-string to the known-literal union — only known
			// ranks (1/2/3) reach this branch, so the cast is safe.
			bestSeverity = item.severity as 'error' | 'warning' | 'info';
		}
	}
	return bestSeverity;
}

/**
 * Look up the severity-max for a node or edge id from the validation store's
 * `nodeSeverityById` / `edgeSeverityById` map. Returns `undefined` when the id
 * is absent — used by the workbench card warning-dot affordance to decide
 * "render dot" vs "render nothing" without leaking the map shape into the
 * components.
 *
 * Centralised so the two card call sites (and any future card consumer) stay
 * aligned on the "key-missing → no dot" semantics. The store guarantees only
 * known severities populate the map, so the return type narrows to the
 * three-literal union plus `undefined`.
 */
export function pickWarningSeverity(
	map: Record<string, 'error' | 'warning' | 'info'>,
	id: string,
): 'error' | 'warning' | 'info' | undefined {
	return map[id];
}

/**
 * Dependencies the pure click-handler needs from the runtime stores. Injected
 * so the helper stays pure and vitest-coverable: every reachable branch of the
 * click flow can be exercised without booting Svelte's reactive runtime.
 *
 * The shape mirrors the methods the validation panel reaches for:
 *   - `pin(id, kind?)`: ensure-pinned semantics for nodes (idempotent — does
 *     NOT toggle). Matches `workbench.pin` directly.
 *   - `bringEdgeToFront(from, to)`: ensure-pinned plus move-to-end for edges,
 *     so the cross-link "last-pinned wins" convention focuses on the just-
 *     clicked edge regardless of previous pin state. Matches
 *     `workbench.bringEdgeToFront`.
 *   - `setSelectedCell(nodeId, bin)`: selection setter for the workbench-card
 *     title cross-highlight. Matches `viewState.setSelectedCell`.
 *   - `currentBin`: the current scrubber bin used as the selection's bin
 *     anchor. Matches `viewState.currentBin`.
 */
export interface ValidationRowClickDeps {
	pin(id: string, kind?: string): void;
	bringEdgeToFront(from: string, to: string): void;
	setSelectedCell(nodeId: string, bin: number): void;
	currentBin: number;
}

/**
 * Pure click-handler for a validation panel row. Encodes the **ensure-pinned +
 * always-set-selection** semantic the panel needs (re-clicking an already-
 * pinned warning row must bring focus back to it without unpinning anything).
 *
 * Branches:
 *   - `row.key === null` (warning carries no identity) → no-op.
 *   - `row.kind === 'node'` → `pin(row.key)` (idempotent ensure-pinned) then
 *     **always** `setSelectedCell(row.key, currentBin)` so the workbench-card
 *     title cross-highlight follows the click. Re-clicking the same row is a
 *     selection-anchor reaffirmation, not a destructive toggle.
 *   - `row.kind === 'edge'` with malformed key (no arrow, leading arrow, or
 *     trailing arrow) → no-op (graceful fallback if the analyser persisted a
 *     non-`from→to` format).
 *   - `row.kind === 'edge'` with well-formed key → `bringEdgeToFront(from, to)`
 *     so the cross-link surface focuses on the just-clicked edge regardless of
 *     whether it was pinned previously.
 *
 * The click NEVER unpins: the unpin path is owned by the workbench card's close
 * button, not the validation panel. This guards against a destructive re-click
 * regression (the bug that surfaced during smoke-testing 2026-04-27).
 */
export function handleValidationRowClick(
	row: ValidationRow,
	deps: ValidationRowClickDeps,
): void {
	if (row.key === null) return;
	if (row.kind === 'node') {
		deps.pin(row.key);
		deps.setSelectedCell(row.key, deps.currentBin);
		return;
	}
	// Edge row — parse `from→to` per the workbench's edge-key convention. If
	// the format doesn't match, no-op rather than pin a malformed edge.
	const ARROW = '→';
	const idx = row.key.indexOf(ARROW);
	if (idx <= 0 || idx === row.key.length - 1) return;
	const from = row.key.slice(0, idx);
	const to = row.key.slice(idx + 1);
	deps.bringEdgeToFront(from, to);
}

/**
 * Selection shape consumed by `rowsMatchingSelection`. Either field may be
 * present; passing both is meaningful in principle (the panel highlights
 * rows that match either identity) but the panel today only supplies one
 * at a time.
 */
export interface ValidationSelection {
	nodeId?: string;
	edgeId?: string;
}

/**
 * Stable identity for a row, used by `rowsMatchingSelection` and by the
 * panel's `{#each ... as ... (rowId(row))}` keying.
 *
 * Derivation choice: `${kind}:${key ?? '_'}::${code}::${message}`. We pick a
 * stable composite of the four discriminating fields rather than array index
 * so the identity survives sort / re-derivation of the row list. The
 * separator `::` is unlikely to appear in a code or message; if it does, the
 * worst case is two distinct rows producing the same id, and the panel
 * would key them together — visible but harmless.
 */
export function rowId(row: ValidationRow): string {
	return `${row.kind}:${row.key ?? '_'}::${row.code}::${row.message}`;
}

/**
 * Derive the set of `rowId`s that match the current workbench selection.
 * Used for the bidirectional cross-link (AC10): when a node or edge is
 * selected in the workbench, the panel highlights matching warning rows.
 *
 * Matching rules:
 * - `selection === null` → empty set.
 * - `selection.nodeId` set → match node-kind rows whose `key === nodeId`
 *   AND `key !== null` (unkeyed rows never match).
 * - `selection.edgeId` set → match edge-kind rows whose `key === edgeId`
 *   AND `key !== null`.
 * - `selection` with neither field → empty set.
 *
 * The kind discriminator is enforced — an edge row whose key happens to
 * equal a `nodeId` does not match a `nodeId` selection (and vice versa).
 */
export function rowsMatchingSelection(
	items: ValidationRow[],
	selection: ValidationSelection | null,
): Set<string> {
	const out = new Set<string>();
	if (selection === null) return out;
	const { nodeId, edgeId } = selection;
	if (nodeId === undefined && edgeId === undefined) return out;

	for (const item of items) {
		if (item.key === null) continue;
		if (nodeId !== undefined && item.kind === 'node' && item.key === nodeId) {
			out.add(rowId(item));
			continue;
		}
		if (edgeId !== undefined && item.kind === 'edge' && item.key === edgeId) {
			out.add(rowId(item));
		}
	}
	return out;
}
