import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
	deriveValidationData,
	ValidationStore,
	type DerivedValidation,
	type EdgeMetadata,
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

describe('deriveValidationData — edge-key translation (smoke-test fix 2026-04-27)', () => {
	// Regression coverage for the bug surfaced during real-bytes manual smoke
	// testing: backend `BuildEdgeWarnings` keys `edgeWarnings` by the analyser's
	// persisted edge id (e.g. `"source_to_target"` for the lag YAML), but the
	// topology, panel, and edge-card all expect the workbench's `${from}→${to}`
	// convention. The store now accepts an optional `edges` arg and translates
	// raw keys to the unified form before deriving rows + the severity-max map.

	it('translates raw analyser-id edge keys to from→to using the graph edge metadata', () => {
		const response = makeResponse(
			[],
			{
				source_to_target: [
					w('warning', { code: 'edge_behavior_violation_lag', message: 'lag' }),
				],
			},
		);
		const edges: EdgeMetadata[] = [
			{ id: 'source_to_target', from: 'SourceNode', to: 'TargetNode' },
		];
		const d = deriveValidationData(response, edges);

		// Translated key appears in the severity map.
		expect(d.edgeSeverityById['SourceNode→TargetNode']).toBe('warning');
		expect(d.edgeSeverityById['source_to_target']).toBeUndefined();

		// Row carries the translated key — downstream cross-link / row-match
		// reasoning all see the unified form.
		expect(d.rows).toHaveLength(1);
		expect(d.rows[0].kind).toBe('edge');
		expect(d.rows[0].key).toBe('SourceNode→TargetNode');
	});

	it('strips :port suffixes from edge endpoints to match the dag-map data-edge-from / data-edge-to convention', () => {
		// Regression coverage for the second-order smoke-test bug: the live
		// graph response carries port-suffixed identifiers (`SourceNode:out` /
		// `TargetNode:in`) but the dag-map renders `data-edge-from="SourceNode"`
		// (no port). The translator must strip the `:port` suffix when building
		// the from→to target so the topology indicator selector resolves the
		// correct path element. Mirrors `StateQueryService.ExtractNodeReference`.
		const response = makeResponse(
			[],
			{
				source_to_target: [
					w('warning', { code: 'edge_behavior_violation_lag', message: 'lag' }),
				],
			},
		);
		const edges: EdgeMetadata[] = [
			{ id: 'source_to_target', from: 'SourceNode:out', to: 'TargetNode:in' },
		];
		const d = deriveValidationData(response, edges);

		expect(d.edgeSeverityById['SourceNode→TargetNode']).toBe('warning');
		expect(d.edgeSeverityById['SourceNode:out→TargetNode:in']).toBeUndefined();
		expect(d.rows[0].key).toBe('SourceNode→TargetNode');
	});

	it('preserves keys that are already in from→to form when graph metadata exists (mocked-spec compat)', () => {
		// The mocked Playwright specs build hand-rolled edgeWarnings keyed by
		// `${from}→${to}` directly. With graph metadata available, the
		// translator must recognise the already-translated form and pass it
		// through unchanged (no fall-through warn, no key drift).
		const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
		try {
			const response = makeResponse(
				[],
				{
					'SourceNode→TargetNode': [
						w('warning', { code: 'edge_flow_mismatch_outgoing', message: 'm' }),
					],
				},
			);
			const edges: EdgeMetadata[] = [
				{ id: 'source_to_target', from: 'SourceNode', to: 'TargetNode' },
			];
			const d = deriveValidationData(response, edges);
			expect(d.edgeSeverityById['SourceNode→TargetNode']).toBe('warning');
			expect(d.rows[0].key).toBe('SourceNode→TargetNode');
			// No warn — the key matched the from→to side of the map.
			expect(warnSpy).not.toHaveBeenCalled();
		} finally {
			warnSpy.mockRestore();
		}
	});

	it('falls back to the raw key + logs a warn when an unmapped edge id arrives', () => {
		const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
		try {
			const response = makeResponse(
				[],
				{
					ghost_edge_id: [w('warning', { code: 'c', message: 'm' })],
				},
			);
			const edges: EdgeMetadata[] = [
				{ id: 'real_edge', from: 'A', to: 'B' },
			];
			const d = deriveValidationData(response, edges);
			// Raw key preserved → downstream consumers see the unmapped key the
			// same way they did before the translation patch (graceful skip in
			// the topology effect, raw display in the panel row).
			expect(d.edgeSeverityById['ghost_edge_id']).toBe('warning');
			expect(d.rows[0].key).toBe('ghost_edge_id');
			// Debug-aid warn fired once.
			expect(warnSpy).toHaveBeenCalledTimes(1);
			expect(warnSpy.mock.calls[0][0]).toContain('unmapped edge key');
		} finally {
			warnSpy.mockRestore();
		}
	});

	it('preserves raw keys verbatim when edges param is omitted (legacy / mocked-test back-compat)', () => {
		// Existing mocked specs and tests don't pass graph metadata; the store
		// must behave identically to the pre-patch single-arg form.
		const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
		try {
			const response = makeResponse(
				[],
				{
					'a→b': [w('warning', { code: 'c', message: 'm' })],
				},
			);
			const d = deriveValidationData(response);
			expect(d.edgeSeverityById['a→b']).toBe('warning');
			expect(d.rows[0].key).toBe('a→b');
			expect(warnSpy).not.toHaveBeenCalled();
		} finally {
			warnSpy.mockRestore();
		}
	});

	it('treats an empty edges array the same as omitted (identity passthrough)', () => {
		const response = makeResponse(
			[],
			{ raw_id: [w('warning')] },
		);
		const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
		try {
			const d = deriveValidationData(response, []);
			expect(d.edgeSeverityById['raw_id']).toBe('warning');
			expect(warnSpy).not.toHaveBeenCalled();
		} finally {
			warnSpy.mockRestore();
		}
	});

	it('treats a null edges arg the same as omitted (identity passthrough)', () => {
		const response = makeResponse(
			[],
			{ raw_id: [w('warning')] },
		);
		const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
		try {
			const d = deriveValidationData(response, null);
			expect(d.edgeSeverityById['raw_id']).toBe('warning');
			expect(warnSpy).not.toHaveBeenCalled();
		} finally {
			warnSpy.mockRestore();
		}
	});

	it('handles edge metadata without an id (only from/to populated) — no analyser-id key registered, from→to still recognised', () => {
		// Graph edges may not all carry an `id` field (it's optional in the
		// type). The map should still register the from→to side so the
		// already-translated path keeps working.
		const response = makeResponse(
			[],
			{ 'X→Y': [w('warning')] },
		);
		const edges: EdgeMetadata[] = [{ from: 'X', to: 'Y' }];
		const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
		try {
			const d = deriveValidationData(response, edges);
			expect(d.edgeSeverityById['X→Y']).toBe('warning');
			expect(warnSpy).not.toHaveBeenCalled();
		} finally {
			warnSpy.mockRestore();
		}
	});

	it('translates ValidationStore.setResponse(response, edges) keys end-to-end via the rune wrapper', () => {
		const store = new ValidationStore();
		store.setResponse(
			makeResponse(
				[],
				{ source_to_target: [w('warning', { code: 'lag', message: 'lag msg' })] },
			),
			[{ id: 'source_to_target', from: 'SourceNode', to: 'TargetNode' }],
		);
		expect(store.edgeSeverityById['SourceNode→TargetNode']).toBe('warning');
		expect(store.rows[0].key).toBe('SourceNode→TargetNode');
	});

	it('omitting edges from setResponse on a follow-up call resets translation (graph clears with the run)', () => {
		const store = new ValidationStore();
		store.setResponse(
			makeResponse([], { source_to_target: [w('warning')] }),
			[{ id: 'source_to_target', from: 'A', to: 'B' }],
		);
		expect(store.edgeSeverityById['A→B']).toBe('warning');
		// New run begins → setResponse(null) (legacy reset path). No edges arg.
		store.setResponse(null);
		expect(store.edgeSeverityById).toEqual({});
		// Next response arrives without graph metadata (legacy / mocked path) —
		// keys must be raw again.
		store.setResponse(
			makeResponse([], { source_to_target: [w('warning')] }),
		);
		expect(store.edgeSeverityById['source_to_target']).toBe('warning');
		expect(store.edgeSeverityById['A→B']).toBeUndefined();
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
