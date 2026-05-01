import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { flowtime } from './flowtime.js';
import type { StateWarning, StateWindowResponse } from './types.js';

/**
 * Wire-shape round-trip parse for `StateWindowResponse` — m-E21-07 AC1.
 *
 * Mirrors the `TimeTravelStateWarningDto` shape that the Engine actually emits
 * (`src/FlowTime.UI/Services/TimeTravelApiModels.cs:521`) and that
 * `BuildEdgeWarnings` keys at `src/FlowTime.API/Services/StateQueryService.cs:4783`.
 *
 * Drives a fixture JSON object — every widened field populated — through
 * `flowtime.getStateWindow(...)` (the real production deserialization path:
 * `fetch → res.json() → cast to StateWindowResponse` in `client.ts`) so the
 * TypeScript types are exercised exactly as production exercises them.
 *
 * If the type definitions in `./types.ts` drift away from the wire DTO, this
 * suite fails at TypeScript compile time (typed-property access on the
 * widened fields) — that is the contract the test guards.
 */

function fetchStub(body: unknown) {
	return vi.fn(async (_url: RequestInfo | URL, _init?: RequestInit) => ({
		ok: true,
		status: 200,
		json: async () => body,
		text: async () => JSON.stringify(body),
	}) as unknown as Response);
}

// Authoritative fixture shape — every wire field populated so the round-trip
// asserts each widened optional propagates through the typed pipeline.
function fixtureWithEveryFieldPopulated(): StateWindowResponse {
	const warningEveryField: StateWarning = {
		code: 'queue-saturation-projected',
		message: 'Queue saturation projected on node-A bins 5..7 (signal: arrivals).',
		severity: 'warning',
		nodeId: 'node-A',
		startBin: 5,
		endBin: 7,
		signal: 'arrivals',
	};
	const warningErrorSeverity: StateWarning = {
		code: 'retry-budget-exhausted',
		message: 'Retry budget exhausted on node-B at bin 12.',
		severity: 'error',
		nodeId: 'node-B',
		startBin: 12,
		endBin: 12,
		signal: 'retries',
	};
	const warningInfoSeverity: StateWarning = {
		code: 'stationarity-passed',
		message: 'Stationarity passed (informational).',
		severity: 'info',
		// nodeId / startBin / endBin / signal intentionally omitted — every optional
		// must round-trip in the absent state too.
	};
	const edgeWarning: StateWarning = {
		code: 'constraint-edge-tight',
		message: 'Constraint edge node-A→node-B at saturation across bins 3..4.',
		severity: 'warning',
		startBin: 3,
		endBin: 4,
		signal: 'capacity',
	};
	return {
		metadata: {
			runId: 'run-fixture',
			templateId: 'tpl-fixture',
			mode: 'sim',
			schema: { id: 'flowtime.model', version: '1', hash: 'abc' },
			edgeQuality: 'ok',
		},
		window: { startBin: 0, endBin: 23, binCount: 24 },
		timestampsUtc: [],
		nodes: [],
		edges: [],
		warnings: [warningEveryField, warningErrorSeverity, warningInfoSeverity],
		edgeWarnings: {
			'node-A→node-B': [edgeWarning],
		},
	};
}

describe('StateWindowResponse — wire-DTO round-trip parse (AC1)', () => {
	const originalFetch = globalThis.fetch;
	let mockFetch: ReturnType<typeof fetchStub>;

	beforeEach(() => {
		mockFetch = fetchStub(fixtureWithEveryFieldPopulated());
		globalThis.fetch = mockFetch as unknown as typeof fetch;
	});

	afterEach(() => {
		globalThis.fetch = originalFetch;
	});

	it('round-trips every widened StateWarning field (severity, startBin, endBin, signal, nodeId)', async () => {
		const result = await flowtime.getStateWindow('run-fixture', 0, 23);
		expect(result.success).toBe(true);
		const value = result.value;
		if (!value) throw new Error('expected populated value');

		expect(value.warnings).toHaveLength(3);
		const [first, second, third] = value.warnings;

		// First — every field populated.
		expect(first.code).toBe('queue-saturation-projected');
		expect(first.message).toContain('Queue saturation');
		expect(first.severity).toBe('warning');
		expect(first.nodeId).toBe('node-A');
		expect(first.startBin).toBe(5);
		expect(first.endBin).toBe(7);
		expect(first.signal).toBe('arrivals');

		// Second — error severity, identifies that severity is a free string per the
		// wire DTO (default "warning"); the type permits the whole literal set the
		// backend may emit.
		expect(second.severity).toBe('error');
		expect(second.nodeId).toBe('node-B');
		expect(second.startBin).toBe(12);
		expect(second.endBin).toBe(12);
		expect(second.signal).toBe('retries');

		// Third — info severity with all optionals absent. Asserts each optional
		// round-trips cleanly in the unpopulated state too.
		expect(third.severity).toBe('info');
		expect(third.nodeId).toBeUndefined();
		expect(third.startBin).toBeUndefined();
		expect(third.endBin).toBeUndefined();
		expect(third.signal).toBeUndefined();
	});

	it('declares edgeWarnings as a Record<string, StateWarning[]> on StateWindowResponse', async () => {
		const result = await flowtime.getStateWindow('run-fixture', 0, 23);
		expect(result.success).toBe(true);
		const value = result.value;
		if (!value) throw new Error('expected populated value');

		// Typed access — the TypeScript compiler enforces edgeWarnings is on the
		// response type. If the type drifts, this file fails to type-check.
		const edgeWarnings = value.edgeWarnings;
		expect(edgeWarnings).toBeDefined();
		expect(Object.keys(edgeWarnings)).toEqual(['node-A→node-B']);
		const edgeRow = edgeWarnings['node-A→node-B'][0];
		expect(edgeRow.code).toBe('constraint-edge-tight');
		expect(edgeRow.message).toContain('node-A→node-B');
		expect(edgeRow.severity).toBe('warning');
		expect(edgeRow.startBin).toBe(3);
		expect(edgeRow.endBin).toBe(4);
		expect(edgeRow.signal).toBe('capacity');
		// nodeId is absent on edge-keyed warnings — identity comes from the map key.
		expect(edgeRow.nodeId).toBeUndefined();
	});

	it('round-trips an empty edgeWarnings map (covers the no-edge-warnings branch)', async () => {
		const empty: StateWindowResponse = {
			metadata: {
				runId: 'run-empty',
				templateId: 'tpl-empty',
				mode: 'sim',
				schema: { id: 'flowtime.model', version: '1', hash: 'abc' },
				edgeQuality: 'ok',
			},
			window: { startBin: 0, endBin: 0, binCount: 1 },
			timestampsUtc: [],
			nodes: [],
			edges: [],
			warnings: [],
			edgeWarnings: {},
		};
		mockFetch = fetchStub(empty);
		globalThis.fetch = mockFetch as unknown as typeof fetch;

		const result = await flowtime.getStateWindow('run-empty', 0, 0);
		expect(result.success).toBe(true);
		const value = result.value;
		if (!value) throw new Error('expected populated value');

		expect(value.warnings).toEqual([]);
		expect(value.edgeWarnings).toEqual({});
	});

	it('rejects the obsolete `bins?: number[]` phantom field on StateWarning at compile time', () => {
		// This test asserts via type-only construction: building a StateWarning and
		// confirming the property surface excludes the phantom `bins` field. The
		// `keyof StateWarning` union is checked against the expected widened shape;
		// if `bins` is reintroduced, this expectation flips at compile time.
		type StateWarningKeys = keyof StateWarning;
		const expectedKeys = [
			'code',
			'message',
			'severity',
			'nodeId',
			'startBin',
			'endBin',
			'signal',
		] as const;
		// Compile-time guard: every member of expectedKeys is a key of StateWarning.
		const keysInWarning: StateWarningKeys[] = [...expectedKeys];
		expect(keysInWarning).toHaveLength(7);

		// Compile-time guard: `bins` is NOT a key of StateWarning. If it were,
		// the conditional type below would resolve to `never` instead of
		// `'bins-is-not-on-StateWarning'`.
		type BinsGuard = 'bins' extends keyof StateWarning
			? 'bins-LEAKED-back-onto-StateWarning'
			: 'bins-is-not-on-StateWarning';
		const guard: BinsGuard = 'bins-is-not-on-StateWarning';
		expect(guard).toBe('bins-is-not-on-StateWarning');
	});
});
