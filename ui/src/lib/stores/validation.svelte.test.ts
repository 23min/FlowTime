import { describe, it, expect, beforeEach } from 'vitest';
import {
	deriveValidationData,
	ValidationStore,
	type DerivedValidation,
} from './validation.svelte.js';
import { rowId } from '$lib/utils/validation-helpers.js';
import type { StateWindowResponse, StateWarning } from '$lib/api/types.js';

/**
 * Validation store — m-E21-07 AC11.
 *
 * Single source of truth for the validation rows + per-node + per-edge
 * severity-max maps derived from a `state_window` response. The store does
 * not fetch; the route owns the fetch and pushes the response into the store
 * after each `loadWindow()` call.
 *
 * The pure `deriveValidationData(response | null)` function is the seam under
 * test: the rune-backed `ValidationStore` wraps it and re-derives whenever
 * `setResponse(...)` mutates the held response. Vitest covers the derivation
 * surface line-by-line; the rune wrapper is exercised through a thin smoke
 * test (rune state itself is the rune runtime's responsibility).
 */

function w(
	severity: string,
	overrides: Partial<StateWarning> = {},
): StateWarning {
	return {
		code: overrides.code ?? 'c',
		message: overrides.message ?? 'm',
		severity,
		nodeId: overrides.nodeId,
		startBin: overrides.startBin,
		endBin: overrides.endBin,
		signal: overrides.signal,
	};
}

function makeResponse(
	warnings: StateWarning[],
	edgeWarnings: Record<string, StateWarning[]> = {},
): StateWindowResponse {
	return {
		metadata: {
			runId: 'r',
			templateId: 't',
			mode: 'sim',
			schema: { id: 'flowtime.model', version: '1', hash: 'abc' },
			edgeQuality: 'ok',
		},
		window: { startBin: 0, endBin: 0, binCount: 1 },
		timestampsUtc: [],
		nodes: [],
		edges: [],
		warnings,
		edgeWarnings,
	};
}

describe('deriveValidationData — null + empty branches', () => {
	it('returns the empty derivation when response is null (no run loaded)', () => {
		const d: DerivedValidation = deriveValidationData(null);
		expect(d.state).toBe('empty');
		expect(d.rows).toEqual([]);
		expect(d.nodeSeverityById).toEqual({});
		expect(d.edgeSeverityById).toEqual({});
	});

	it('returns the empty derivation when response has zero warnings + zero edgeWarnings', () => {
		const d = deriveValidationData(makeResponse([], {}));
		expect(d.state).toBe('empty');
		expect(d.rows).toEqual([]);
		expect(d.nodeSeverityById).toEqual({});
		expect(d.edgeSeverityById).toEqual({});
	});
});

describe('deriveValidationData — node warning rows', () => {
	it('flattens warnings[] into node-kind rows and copies the wire fields', () => {
		const response = makeResponse([
			w('warning', {
				code: 'queue-sat',
				message: 'queue saturating',
				nodeId: 'node-A',
				startBin: 5,
				endBin: 7,
				signal: 'arrivals',
			}),
		]);
		const d = deriveValidationData(response);
		expect(d.state).toBe('issues');
		expect(d.rows).toHaveLength(1);
		const row = d.rows[0];
		expect(row.kind).toBe('node');
		expect(row.key).toBe('node-A');
		expect(row.severity).toBe('warning');
		expect(row.code).toBe('queue-sat');
		expect(row.message).toBe('queue saturating');
		expect(row.startBin).toBe(5);
		expect(row.endBin).toBe(7);
		expect(row.signal).toBe('arrivals');
	});

	it('treats a node warning without nodeId as a row whose key is null (no pin affordance)', () => {
		const response = makeResponse([
			w('warning', { code: 'global', message: 'no node attribution' }),
		]);
		const d = deriveValidationData(response);
		expect(d.rows).toHaveLength(1);
		expect(d.rows[0].kind).toBe('node');
		expect(d.rows[0].key).toBeNull();
	});

	it('produces one row per warning when multiple warnings target the same node', () => {
		const response = makeResponse([
			w('warning', { code: 'a', message: 'first', nodeId: 'node-A' }),
			w('error', { code: 'b', message: 'second', nodeId: 'node-A' }),
		]);
		const d = deriveValidationData(response);
		expect(d.rows).toHaveLength(2);
	});
});

describe('deriveValidationData — edge warning rows', () => {
	it('flattens edgeWarnings into edge-kind rows keyed by the edge id', () => {
		const response = makeResponse(
			[],
			{
				'node-A→node-B': [
					w('warning', {
						code: 'edge-tight',
						message: 'edge tight',
						startBin: 3,
						endBin: 4,
						signal: 'capacity',
					}),
				],
			},
		);
		const d = deriveValidationData(response);
		expect(d.state).toBe('issues');
		expect(d.rows).toHaveLength(1);
		const row = d.rows[0];
		expect(row.kind).toBe('edge');
		expect(row.key).toBe('node-A→node-B');
		expect(row.severity).toBe('warning');
		expect(row.code).toBe('edge-tight');
		expect(row.message).toBe('edge tight');
		expect(row.startBin).toBe(3);
		expect(row.endBin).toBe(4);
		expect(row.signal).toBe('capacity');
	});

	it('produces one row per warning per edge when an edge has multiple warnings', () => {
		const response = makeResponse(
			[],
			{
				'a→b': [
					w('warning', { code: 'first' }),
					w('error', { code: 'second' }),
				],
			},
		);
		const d = deriveValidationData(response);
		expect(d.rows).toHaveLength(2);
	});

	it('skips edge keys whose value array is empty (defensive — value-presence required for rows)', () => {
		// classifyValidationState treats key-presence as "issues" for the panel
		// state, but row generation requires at least one warning to render.
		const response = makeResponse(
			[],
			{
				'empty-key': [],
				'real-key': [w('warning', { code: 'a' })],
			},
		);
		const d = deriveValidationData(response);
		// Two rows? No — only the populated edge produces rows.
		expect(d.rows).toHaveLength(1);
		expect(d.rows[0].key).toBe('real-key');
		// State is still "issues" because at least one row exists.
		expect(d.state).toBe('issues');
	});
});

describe('deriveValidationData — combined node + edge rows + sort order', () => {
	it('combines node and edge rows in a single sorted list (severity desc, then key, then message)', () => {
		const response = makeResponse(
			[
				w('info', { code: 'i1', message: 'info-msg', nodeId: 'node-Z' }),
				w('error', { code: 'e1', message: 'err-msg', nodeId: 'node-A' }),
			],
			{
				'node-A→node-B': [
					w('warning', { code: 'w1', message: 'edge-warn' }),
				],
			},
		);
		const d = deriveValidationData(response);
		expect(d.rows).toHaveLength(3);
		expect(d.rows.map((r) => r.severity)).toEqual(['error', 'warning', 'info']);
	});

	it('row identities (rowId) are stable for downstream cross-link consumers', () => {
		const response = makeResponse([
			w('warning', { code: 'a', message: 'm', nodeId: 'node-A' }),
		]);
		const d = deriveValidationData(response);
		expect(d.rows).toHaveLength(1);
		const id = rowId(d.rows[0]);
		expect(id).toContain('node:node-A');
		expect(id).toContain('::a::m');
	});
});

describe('deriveValidationData — severity-max maps', () => {
	it('produces a node severity map keyed by nodeId with the most-severe warning per node', () => {
		const response = makeResponse([
			w('info', { nodeId: 'n1' }),
			w('warning', { nodeId: 'n1' }),
			w('error', { nodeId: 'n2' }),
			w('warning', { nodeId: 'n3' }),
		]);
		const d = deriveValidationData(response);
		expect(d.nodeSeverityById['n1']).toBe('warning'); // info < warning
		expect(d.nodeSeverityById['n2']).toBe('error');
		expect(d.nodeSeverityById['n3']).toBe('warning');
	});

	it('does not add a node severity entry for warnings with no nodeId', () => {
		const response = makeResponse([
			w('error', { code: 'global' }),
			w('warning', { nodeId: 'n1' }),
		]);
		const d = deriveValidationData(response);
		expect(d.nodeSeverityById).toEqual({ n1: 'warning' });
	});

	it('produces an edge severity map keyed by edge id with the most-severe warning per edge', () => {
		const response = makeResponse(
			[],
			{
				'a→b': [w('info'), w('warning')],
				'c→d': [w('error'), w('warning')],
			},
		);
		const d = deriveValidationData(response);
		expect(d.edgeSeverityById['a→b']).toBe('warning');
		expect(d.edgeSeverityById['c→d']).toBe('error');
	});

	it('omits edge severity entries for edges whose warnings only carry unknown severities', () => {
		const response = makeResponse(
			[],
			{
				'a→b': [w('mystery'), w('alien')],
			},
		);
		const d = deriveValidationData(response);
		expect(d.edgeSeverityById['a→b']).toBeUndefined();
	});

	it('omits node severity entries for nodes whose warnings only carry unknown severities', () => {
		const response = makeResponse([
			w('mystery', { nodeId: 'n1' }),
		]);
		const d = deriveValidationData(response);
		expect(d.nodeSeverityById['n1']).toBeUndefined();
	});

	it('omits edge severity entries for edges with empty warning arrays', () => {
		const response = makeResponse([], { 'empty-edge': [] });
		const d = deriveValidationData(response);
		expect(d.edgeSeverityById['empty-edge']).toBeUndefined();
	});

	it('does not demote an existing node severity when a less-severe later warning arrives (warning then info → stays warning)', () => {
		// Order matters for the promoteSeverity branch: warning is set first,
		// then info arrives second. The "nextRank <= currentRank" branch must
		// be exercised — we should still see "warning" in the map.
		const response = makeResponse([
			w('warning', { nodeId: 'n1' }),
			w('info', { nodeId: 'n1' }),
		]);
		const d = deriveValidationData(response);
		expect(d.nodeSeverityById['n1']).toBe('warning');
	});

	it('does not demote an existing edge severity when a less-severe later warning arrives', () => {
		const response = makeResponse(
			[],
			{ 'a→b': [w('error'), w('info')] },
		);
		const d = deriveValidationData(response);
		expect(d.edgeSeverityById['a→b']).toBe('error');
	});

	it('ignores unknown severities on the node-severity path without dropping the existing winner', () => {
		const response = makeResponse([
			w('warning', { nodeId: 'n1' }),
			w('mystery', { nodeId: 'n1' }),
		]);
		const d = deriveValidationData(response);
		expect(d.nodeSeverityById['n1']).toBe('warning');
	});
});

describe('ValidationStore — rune wrapper', () => {
	let store: ValidationStore;
	beforeEach(() => {
		store = new ValidationStore();
	});

	it('starts with the empty derivation', () => {
		expect(store.state).toBe('empty');
		expect(store.rows).toEqual([]);
		expect(store.nodeSeverityById).toEqual({});
		expect(store.edgeSeverityById).toEqual({});
	});

	it('setResponse(response) updates the derived rows and severity maps', () => {
		store.setResponse(
			makeResponse(
				[w('error', { nodeId: 'n1' })],
				{ 'a→b': [w('warning')] },
			),
		);
		expect(store.state).toBe('issues');
		expect(store.rows).toHaveLength(2);
		expect(store.nodeSeverityById['n1']).toBe('error');
		expect(store.edgeSeverityById['a→b']).toBe('warning');
	});

	it('setResponse(null) resets to the empty derivation (used when a new run begins loading)', () => {
		store.setResponse(makeResponse([w('error', { nodeId: 'n1' })]));
		expect(store.rows).toHaveLength(1);
		store.setResponse(null);
		expect(store.state).toBe('empty');
		expect(store.rows).toEqual([]);
		expect(store.nodeSeverityById).toEqual({});
		expect(store.edgeSeverityById).toEqual({});
	});

	it('setResponse with the same response does not re-derive when shallow-equal (smoke — derivation always re-runs but result shape stays identical)', () => {
		const r = makeResponse([w('warning', { nodeId: 'n1' })]);
		store.setResponse(r);
		const rowsBefore = store.rows;
		store.setResponse(r);
		// Result must remain a deep-equal list (we don't assert reference equality —
		// the rune wrapper re-runs derivation on every set, which is fine).
		expect(store.rows).toEqual(rowsBefore);
	});
});
