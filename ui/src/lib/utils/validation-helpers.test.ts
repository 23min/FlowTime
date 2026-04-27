import { describe, it, expect } from 'vitest';
import {
	classifyValidationState,
	handleValidationRowClick,
	maxSeverityForKey,
	pickWarningSeverity,
	rowId,
	rowsMatchingSelection,
	severityChromeToken,
	sortValidationItems,
	type ValidationRow,
	type ValidationRowClickDeps,
} from './validation-helpers.js';
import type { StateWarning } from '../api/types.js';

/**
 * Pure-helper suites for the m-E21-07 Validation Surface (AC14).
 *
 * Each helper is exercised against every reachable conditional branch — the
 * hard rule from the milestone spec is "every reachable branch covered by an
 * explicit assertion." Each `describe` block maps to one of the five vitest
 * suites enumerated in AC14.
 */

describe('classifyValidationState — AC4 / Suite 2', () => {
	it('returns "empty" when both warnings and edgeWarnings are absent', () => {
		const state = classifyValidationState({ warnings: [], edgeWarnings: {} });
		expect(state).toBe('empty');
	});

	it('returns "issues" when warnings array has at least one item', () => {
		const w: StateWarning = { code: 'c', message: 'm', severity: 'warning' };
		const state = classifyValidationState({ warnings: [w], edgeWarnings: {} });
		expect(state).toBe('issues');
	});

	it('returns "issues" when edgeWarnings has at least one populated key', () => {
		const w: StateWarning = { code: 'c', message: 'm', severity: 'warning' };
		const state = classifyValidationState({
			warnings: [],
			edgeWarnings: { 'a→b': [w] },
		});
		expect(state).toBe('issues');
	});

	it('returns "issues" when edgeWarnings has a key whose value array is empty (non-empty key set)', () => {
		// The API only emits edgeWarnings keys when warnings exist (per
		// BuildEdgeWarnings in StateQueryService). If a key surfaces with an
		// empty array — defensive shape — we still treat it as "issues" to
		// match the wire contract: presence of a key means the analyser flagged
		// that edge. The classifier treats key-presence, not value-population,
		// as the empty signal.
		const state = classifyValidationState({
			warnings: [],
			edgeWarnings: { 'a→b': [] },
		});
		expect(state).toBe('issues');
	});

	it('returns "issues" when both warnings and edgeWarnings are populated', () => {
		const w: StateWarning = { code: 'c', message: 'm', severity: 'warning' };
		const state = classifyValidationState({
			warnings: [w],
			edgeWarnings: { 'a→b': [w] },
		});
		expect(state).toBe('issues');
	});
});

describe('sortValidationItems — AC2 / Suite 3', () => {
	function row(
		kind: 'node' | 'edge',
		key: string | null,
		severity: string,
		message: string,
	): ValidationRow {
		return { kind, key, severity, message, code: 'c' };
	}

	it('sorts severity descending (error > warning > info > unknown)', () => {
		const items: ValidationRow[] = [
			row('node', 'a', 'info', 'm'),
			row('node', 'a', 'mystery', 'm'),
			row('node', 'a', 'error', 'm'),
			row('node', 'a', 'warning', 'm'),
		];
		const sorted = sortValidationItems(items).map((r) => r.severity);
		expect(sorted).toEqual(['error', 'warning', 'info', 'mystery']);
	});

	it('does not mutate the input array', () => {
		const items: ValidationRow[] = [
			row('node', 'b', 'info', 'm'),
			row('node', 'a', 'error', 'm'),
		];
		const snapshot = items.slice();
		sortValidationItems(items);
		expect(items).toEqual(snapshot);
	});

	it('breaks severity ties by key ascending (node-key)', () => {
		const items: ValidationRow[] = [
			row('node', 'zebra', 'warning', 'm'),
			row('node', 'apple', 'warning', 'm'),
			row('node', 'mango', 'warning', 'm'),
		];
		const sorted = sortValidationItems(items).map((r) => r.key);
		expect(sorted).toEqual(['apple', 'mango', 'zebra']);
	});

	it('breaks severity ties by key ascending (edge-key sorted alongside node-key)', () => {
		const items: ValidationRow[] = [
			row('edge', 'beta→gamma', 'warning', 'm'),
			row('node', 'alpha', 'warning', 'm'),
		];
		// Both rows are 'warning'; key precedence is alphabetical (alpha < beta→gamma).
		// The kind discriminator does not influence the sort — keyed identity does.
		const sorted = sortValidationItems(items).map((r) => r.key);
		expect(sorted).toEqual(['alpha', 'beta→gamma']);
	});

	it('sorts empty / null keys after non-empty keys at the same severity', () => {
		const items: ValidationRow[] = [
			row('node', null, 'warning', 'b'),
			row('node', 'a-node', 'warning', 'a'),
			row('node', '', 'warning', 'c'),
		];
		const sorted = sortValidationItems(items);
		expect(sorted[0].key).toBe('a-node');
		// Both '' and null sort to the tail (treated identically); message
		// breaks the tie within that empty bucket.
		expect(sorted[1].key === '' || sorted[1].key === null).toBe(true);
		expect(sorted[2].key === '' || sorted[2].key === null).toBe(true);
		// The empty-key bucket is then ordered by message (b before c).
		expect(sorted[1].message).toBe('b');
		expect(sorted[2].message).toBe('c');
	});

	it('breaks key ties by message ascending', () => {
		const items: ValidationRow[] = [
			row('node', 'shared', 'warning', 'zebra message'),
			row('node', 'shared', 'warning', 'apple message'),
			row('node', 'shared', 'warning', 'mango message'),
		];
		const sorted = sortValidationItems(items).map((r) => r.message);
		expect(sorted).toEqual(['apple message', 'mango message', 'zebra message']);
	});

	it('returns an empty array when given an empty array', () => {
		expect(sortValidationItems([])).toEqual([]);
	});
});

describe('severityChromeToken — AC7 / AC8 / AC12 / Suite 4', () => {
	it('maps severity "error" to --ft-err', () => {
		expect(severityChromeToken('error')).toBe('--ft-err');
	});

	it('maps severity "warning" to --ft-warn', () => {
		expect(severityChromeToken('warning')).toBe('--ft-warn');
	});

	it('maps severity "info" to --ft-info', () => {
		// Per Q5 = C: ship --ft-info mapping now (the chrome token itself is
		// added in the panel-styling change later in this milestone).
		expect(severityChromeToken('info')).toBe('--ft-info');
	});

	it('returns null for an unknown severity literal (default chrome treatment)', () => {
		expect(severityChromeToken('mystery')).toBeNull();
	});

	it('returns null for the empty string (no severity)', () => {
		// Defensive — the wire DTO defaults severity to "warning" so '' should
		// never arrive, but the helper falls through to the default branch
		// rather than crashing if it does.
		expect(severityChromeToken('')).toBeNull();
	});
});

describe('maxSeverityForKey — AC7 / AC8 / Suite 5', () => {
	function w(severity: string): StateWarning {
		return { code: 'c', message: 'm', severity };
	}

	it('returns null for an empty array', () => {
		expect(maxSeverityForKey([])).toBeNull();
	});

	it('returns "error" for a single error item', () => {
		expect(maxSeverityForKey([w('error')])).toBe('error');
	});

	it('returns "warning" for a single warning item', () => {
		expect(maxSeverityForKey([w('warning')])).toBe('warning');
	});

	it('returns "info" for a single info item', () => {
		expect(maxSeverityForKey([w('info')])).toBe('info');
	});

	it('promotes warning over info when both present', () => {
		expect(maxSeverityForKey([w('info'), w('warning')])).toBe('warning');
	});

	it('promotes error over warning when both present', () => {
		expect(maxSeverityForKey([w('warning'), w('error')])).toBe('error');
	});

	it('promotes error over info when both present', () => {
		expect(maxSeverityForKey([w('info'), w('error')])).toBe('error');
	});

	it('returns "error" when all three known severities are present', () => {
		expect(maxSeverityForKey([w('info'), w('warning'), w('error')])).toBe('error');
	});

	it('does not promote past known literals when an unknown severity is present', () => {
		// Unknown severities cannot win — they're below the lowest known rank
		// (`info`). A pure-unknown bucket returns null.
		expect(maxSeverityForKey([w('mystery'), w('info')])).toBe('info');
	});

	it('returns null when only unknown severities are present', () => {
		expect(maxSeverityForKey([w('mystery'), w('alien')])).toBeNull();
	});

	it('handles repeated identical severities', () => {
		expect(maxSeverityForKey([w('warning'), w('warning'), w('warning')])).toBe('warning');
	});
});

describe('rowsMatchingSelection — AC10 / Suite 6', () => {
	function row(
		kind: 'node' | 'edge',
		key: string | null,
		severity: string,
		message: string,
		code = 'c',
	): ValidationRow {
		return { kind, key, severity, message, code };
	}

	const items: ValidationRow[] = [
		row('node', 'node-A', 'warning', 'queue saturating', 'queue-sat'),
		row('node', 'node-B', 'error', 'retry budget exhausted', 'retry'),
		row('edge', 'node-A→node-B', 'warning', 'edge tight', 'edge-tight'),
		row('node', null, 'info', 'unkeyed informational', 'info-unkeyed'),
	];

	it('returns the rowId set for a node selection', () => {
		const result = rowsMatchingSelection(items, { nodeId: 'node-A' });
		expect(result.size).toBe(1);
		expect(result.has(rowId(items[0]))).toBe(true);
	});

	it('returns the rowId set for an edge selection', () => {
		const result = rowsMatchingSelection(items, { edgeId: 'node-A→node-B' });
		expect(result.size).toBe(1);
		expect(result.has(rowId(items[2]))).toBe(true);
	});

	it('returns an empty set when nothing matches the node selection', () => {
		const result = rowsMatchingSelection(items, { nodeId: 'node-Z' });
		expect(result.size).toBe(0);
	});

	it('returns an empty set when nothing matches the edge selection', () => {
		const result = rowsMatchingSelection(items, { edgeId: 'node-X→node-Y' });
		expect(result.size).toBe(0);
	});

	it('returns an empty set when selection is null', () => {
		const result = rowsMatchingSelection(items, null);
		expect(result.size).toBe(0);
	});

	it('returns an empty set when selection has neither nodeId nor edgeId', () => {
		const result = rowsMatchingSelection(items, {});
		expect(result.size).toBe(0);
	});

	it('does not match an edge row when the selection is a nodeId equal to that edge key', () => {
		// Defensive — node-kind matching is gated on `kind === 'node'`. An
		// edge row whose key happens to be the same string the user passed as
		// `nodeId` must not match.
		const tricky: ValidationRow[] = [
			row('edge', 'node-A', 'warning', 'edge-shaped key, but kind=edge', 'tricky'),
		];
		const result = rowsMatchingSelection(tricky, { nodeId: 'node-A' });
		expect(result.size).toBe(0);
	});

	it('does not match a node row when the selection is an edgeId equal to that node key', () => {
		const tricky: ValidationRow[] = [
			row('node', 'node-A→node-B', 'warning', 'node with edge-shaped key', 'tricky'),
		];
		const result = rowsMatchingSelection(tricky, { edgeId: 'node-A→node-B' });
		expect(result.size).toBe(0);
	});

	it('does not match an unkeyed (key === null) node row even when nodeId is provided', () => {
		// Unkeyed rows have no clickable identity per AC9; the selection logic
		// must not surface them under any selection.
		const result = rowsMatchingSelection(items, { nodeId: 'node-A' });
		expect(result.has(rowId(items[3]))).toBe(false);
	});

	it('matches multiple rows that share the same nodeId', () => {
		const multi: ValidationRow[] = [
			row('node', 'node-A', 'warning', 'first', 'c1'),
			row('node', 'node-A', 'error', 'second', 'c2'),
			row('node', 'node-B', 'warning', 'other', 'c3'),
		];
		const result = rowsMatchingSelection(multi, { nodeId: 'node-A' });
		expect(result.size).toBe(2);
		expect(result.has(rowId(multi[0]))).toBe(true);
		expect(result.has(rowId(multi[1]))).toBe(true);
		expect(result.has(rowId(multi[2]))).toBe(false);
	});

	it('returns rowIds that are stable across calls (same input → same string)', () => {
		const id1 = rowId(items[0]);
		const id2 = rowId(items[0]);
		expect(id1).toBe(id2);
		// Distinct rows produce distinct ids.
		expect(rowId(items[0])).not.toBe(rowId(items[1]));
	});
});

describe('pickWarningSeverity — workbench card warning dot', () => {
	/**
	 * The workbench `WorkbenchCard` and `WorkbenchEdgeCard` consume this helper
	 * to derive the per-card severity dot from `validation.nodeSeverityById` /
	 * `validation.edgeSeverityById`. Pure passthrough plus an `undefined`
	 * fallback for missing ids — keeping the lookup centralised so both card
	 * call sites (and any future card consumer) cannot drift on the
	 * "key-missing → no dot" semantics.
	 */
	const map: Record<string, 'error' | 'warning' | 'info'> = {
		'node-A': 'warning',
		'node-B': 'error',
		'node-C': 'info',
	};

	it('returns the severity for a present id', () => {
		expect(pickWarningSeverity(map, 'node-A')).toBe('warning');
		expect(pickWarningSeverity(map, 'node-B')).toBe('error');
		expect(pickWarningSeverity(map, 'node-C')).toBe('info');
	});

	it('returns undefined when the id is absent from the map', () => {
		expect(pickWarningSeverity(map, 'node-Z')).toBeUndefined();
	});

	it('returns undefined for an empty map', () => {
		expect(pickWarningSeverity({}, 'node-A')).toBeUndefined();
	});

	it('returns undefined when the id is the empty string and the map has no empty key', () => {
		// Defensive — an empty-string id should not collide with a missing entry.
		expect(pickWarningSeverity(map, '')).toBeUndefined();
	});
});

describe('handleValidationRowClick — re-click bug fix (2026-04-27)', () => {
	/**
	 * Regression coverage for the smoke-test bug: clicking a previously-clicked
	 * warning row used to skip `setSelectedCell` because of an `if (!wasPinned)`
	 * guard, leaving the selection stranded on the most-recently-clicked row.
	 * The fix is "ensure-pinned + always-set-selection". Re-clicking the same
	 * row is a non-destructive selection-anchor reaffirmation; it never unpins
	 * anything.
	 *
	 * For edges, the symmetric fix is `bringEdgeToFront(from, to)` —
	 * ensure-pinned + move-to-end so the cross-link "last-pinned wins"
	 * convention focuses on the just-clicked edge.
	 *
	 * The deps shape is injected so every branch is exercised against a
	 * recording stub; the runtime-store wiring at the panel call site is a
	 * one-liner that adapts `workbench` / `viewState` to the helper.
	 */
	function makeDepsRecorder(initialBin = 4): ValidationRowClickDeps & {
		// Recording fields exposed for assertions.
		pinCalls: { id: string; kind: string | undefined }[];
		bringEdgeCalls: { from: string; to: string }[];
		setSelectedCellCalls: { nodeId: string; bin: number }[];
	} {
		const pinCalls: { id: string; kind: string | undefined }[] = [];
		const bringEdgeCalls: { from: string; to: string }[] = [];
		const setSelectedCellCalls: { nodeId: string; bin: number }[] = [];
		return {
			currentBin: initialBin,
			pin(id, kind) {
				pinCalls.push({ id, kind });
			},
			bringEdgeToFront(from, to) {
				bringEdgeCalls.push({ from, to });
			},
			setSelectedCell(nodeId, bin) {
				setSelectedCellCalls.push({ nodeId, bin });
			},
			pinCalls,
			bringEdgeCalls,
			setSelectedCellCalls,
		};
	}

	function nodeRow(key: string | null): ValidationRow {
		return { kind: 'node', key, severity: 'warning', message: 'm', code: 'c' };
	}

	function edgeRow(key: string): ValidationRow {
		return { kind: 'edge', key, severity: 'warning', message: 'm', code: 'c' };
	}

	// ---- node row branches ---------------------------------------------------

	it('node row with key calls pin AND setSelectedCell', () => {
		const deps = makeDepsRecorder();
		handleValidationRowClick(nodeRow('node-A'), deps);
		expect(deps.pinCalls).toEqual([{ id: 'node-A', kind: undefined }]);
		expect(deps.setSelectedCellCalls).toEqual([{ nodeId: 'node-A', bin: 4 }]);
		expect(deps.bringEdgeCalls).toEqual([]);
	});

	it('node row with null key is a no-op (no pin, no selection set)', () => {
		const deps = makeDepsRecorder();
		handleValidationRowClick(nodeRow(null), deps);
		expect(deps.pinCalls).toEqual([]);
		expect(deps.setSelectedCellCalls).toEqual([]);
		expect(deps.bringEdgeCalls).toEqual([]);
	});

	it('three-step bug repro for nodes — A → B → A — leaves both pinned and selection on A (the bug fix)', () => {
		// This is the exact smoke-test scenario from 2026-04-27. Pre-fix, step 3
		// silently skipped setSelectedCell (because of an `if (!wasPinned)`
		// guard) and the selection stayed on B. Post-fix, every click reaffirms
		// the selection anchor.
		const deps = makeDepsRecorder();
		handleValidationRowClick(nodeRow('A'), deps);
		handleValidationRowClick(nodeRow('B'), deps);
		handleValidationRowClick(nodeRow('A'), deps);

		// Pin called every time (idempotent — `workbench.pin` short-circuits
		// duplicates downstream; the helper still invokes it).
		expect(deps.pinCalls.map((c) => c.id)).toEqual(['A', 'B', 'A']);

		// Selection set every time. Critically, the third call sets selection
		// back to A — pre-fix this was missing.
		expect(deps.setSelectedCellCalls.map((c) => c.nodeId)).toEqual([
			'A',
			'B',
			'A',
		]);

		// No edge work happened.
		expect(deps.bringEdgeCalls).toEqual([]);
	});

	it('clicking the same row twice in a row sets selection both times (selection stays on that node)', () => {
		// User asked specifically for this case. Re-clicking the currently-
		// selected row is a no-op selection-wise (because the value is the
		// same), but the helper still calls setSelectedCell — that's harmless,
		// preserves the contract, and lets the runtime decide whether to ignore
		// equal-value sets.
		const deps = makeDepsRecorder();
		handleValidationRowClick(nodeRow('A'), deps);
		handleValidationRowClick(nodeRow('A'), deps);
		expect(deps.setSelectedCellCalls.map((c) => c.nodeId)).toEqual(['A', 'A']);
		expect(deps.pinCalls.map((c) => c.id)).toEqual(['A', 'A']);
	});

	it('uses the deps.currentBin value at call time as the selection bin anchor', () => {
		const deps = makeDepsRecorder(7);
		handleValidationRowClick(nodeRow('A'), deps);
		expect(deps.setSelectedCellCalls).toEqual([{ nodeId: 'A', bin: 7 }]);
	});

	// ---- edge row branches ---------------------------------------------------

	it('edge row with well-formed key calls bringEdgeToFront only (no pin, no selection set)', () => {
		const deps = makeDepsRecorder();
		handleValidationRowClick(edgeRow('A→B'), deps);
		expect(deps.bringEdgeCalls).toEqual([{ from: 'A', to: 'B' }]);
		expect(deps.pinCalls).toEqual([]);
		expect(deps.setSelectedCellCalls).toEqual([]);
	});

	it('three-step bug repro for edges — A→B → C→D → A→B — promotes A→B to last each time', () => {
		// Symmetric to the node repro: clicking an already-pinned edge row
		// must move it to the end of pinnedEdges so the cross-link's
		// last-pinned-wins convention focuses on it. The helper does this via
		// bringEdgeToFront; the workbench-store unit test covers the actual
		// re-ordering. Here we just assert the helper invokes the right
		// method with the right args.
		const deps = makeDepsRecorder();
		handleValidationRowClick(edgeRow('A→B'), deps);
		handleValidationRowClick(edgeRow('C→D'), deps);
		handleValidationRowClick(edgeRow('A→B'), deps);
		expect(deps.bringEdgeCalls).toEqual([
			{ from: 'A', to: 'B' },
			{ from: 'C', to: 'D' },
			{ from: 'A', to: 'B' },
		]);
		expect(deps.pinCalls).toEqual([]);
		expect(deps.setSelectedCellCalls).toEqual([]);
	});

	it('edge row with no arrow is a no-op (graceful fallback for opaque analyser keys)', () => {
		const deps = makeDepsRecorder();
		handleValidationRowClick(edgeRow('opaque-edge-id'), deps);
		expect(deps.bringEdgeCalls).toEqual([]);
		expect(deps.pinCalls).toEqual([]);
	});

	it('edge row with leading arrow is a no-op (no `from` segment)', () => {
		const deps = makeDepsRecorder();
		handleValidationRowClick(edgeRow('→B'), deps);
		expect(deps.bringEdgeCalls).toEqual([]);
	});

	it('edge row with trailing arrow is a no-op (no `to` segment)', () => {
		const deps = makeDepsRecorder();
		handleValidationRowClick(edgeRow('A→'), deps);
		expect(deps.bringEdgeCalls).toEqual([]);
	});

	it('edge row with multi-character node ids around the arrow parses correctly', () => {
		const deps = makeDepsRecorder();
		handleValidationRowClick(edgeRow('queue-1→service-A'), deps);
		expect(deps.bringEdgeCalls).toEqual([
			{ from: 'queue-1', to: 'service-A' },
		]);
	});

	it('edge row with null key is a no-op', () => {
		// Defensive — the panel produces edge rows with non-null keys today
		// (the row builder uses the map key directly), but the helper still
		// has a `key === null` short-circuit shared with node rows.
		const deps = makeDepsRecorder();
		handleValidationRowClick(
			{ kind: 'edge', key: null, severity: 'warning', message: 'm', code: 'c' },
			deps,
		);
		expect(deps.bringEdgeCalls).toEqual([]);
		expect(deps.pinCalls).toEqual([]);
		expect(deps.setSelectedCellCalls).toEqual([]);
	});

	// ---- isolation guarantee -------------------------------------------------

	it('node-row click does NOT touch edge state (edges stay pinned across node interactions)', () => {
		// Constraint check: this fix must not regress topology-toggle behavior.
		// The helper is for the validation panel only; it must never reach into
		// edge state when handling a node row, and vice versa.
		const deps = makeDepsRecorder();
		handleValidationRowClick(nodeRow('A'), deps);
		expect(deps.bringEdgeCalls).toEqual([]);
	});

	it('edge-row click does NOT touch node selection state (selection stays where it was)', () => {
		const deps = makeDepsRecorder();
		handleValidationRowClick(edgeRow('A→B'), deps);
		expect(deps.pinCalls).toEqual([]);
		expect(deps.setSelectedCellCalls).toEqual([]);
	});
});
