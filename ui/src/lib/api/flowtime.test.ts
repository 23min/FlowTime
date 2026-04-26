import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { flowtime } from './flowtime.js';

/**
 * URL-construction tests for `flowtime.getStateWindow` — m-E21-06 AC15.
 *
 * The client gains an optional `mode` parameter (`'operational' | 'full'`). When supplied,
 * the query string includes `&mode=<value>`; when omitted, the URL is unchanged from the
 * pre-milestone shape so existing call sites keep working. All other query parameters
 * remain stable.
 */

function fetchStub(response: { ok: boolean; status: number; body: unknown }) {
	return vi.fn(async (_url: RequestInfo | URL, _init?: RequestInit) => ({
		ok: response.ok,
		status: response.status,
		json: async () => response.body,
		text: async () => JSON.stringify(response.body),
	}) as unknown as Response);
}

describe('flowtime.getStateWindow — URL shape', () => {
	const originalFetch = globalThis.fetch;
	let mockFetch: ReturnType<typeof fetchStub>;

	beforeEach(() => {
		mockFetch = fetchStub({
			ok: true,
			status: 200,
			body: { metadata: {}, window: {}, timestampsUtc: [], nodes: [], edges: [], warnings: [] },
		});
		globalThis.fetch = mockFetch as unknown as typeof fetch;
	});

	afterEach(() => {
		globalThis.fetch = originalFetch;
	});

	it('omits mode from the query string when the parameter is not supplied', async () => {
		await flowtime.getStateWindow('run-1', 0, 23);
		expect(mockFetch).toHaveBeenCalledOnce();
		const [firstCall] = mockFetch.mock.calls;
		const url = String(firstCall[0]);
		expect(url).toBe('/v1/runs/run-1/state_window?startBin=0&endBin=23');
		expect(url).not.toContain('mode=');
	});

	it('appends mode=full when the parameter is supplied', async () => {
		await flowtime.getStateWindow('run-1', 0, 23, 'full');
		const [firstCall] = mockFetch.mock.calls;
		const url = String(firstCall[0]);
		expect(url).toBe('/v1/runs/run-1/state_window?startBin=0&endBin=23&mode=full');
	});

	it('appends mode=operational when explicitly requested', async () => {
		await flowtime.getStateWindow('run-2', 5, 10, 'operational');
		const [firstCall] = mockFetch.mock.calls;
		const url = String(firstCall[0]);
		expect(url).toBe('/v1/runs/run-2/state_window?startBin=5&endBin=10&mode=operational');
	});

	it('omits mode when explicitly passed as undefined (backward-compat)', async () => {
		await flowtime.getStateWindow('run-3', 0, 1, undefined);
		const [firstCall] = mockFetch.mock.calls;
		const url = String(firstCall[0]);
		expect(url).toBe('/v1/runs/run-3/state_window?startBin=0&endBin=1');
	});

	it('URL-encodes the runId (defence against special characters)', async () => {
		await flowtime.getStateWindow('run/a b', 0, 1, 'full');
		const [firstCall] = mockFetch.mock.calls;
		const url = String(firstCall[0]);
		expect(url).toContain(`/state_window?`);
		expect(url).toContain('mode=full');
		// runId with slash + space → slash becomes %2F and space becomes %20.
		expect(url).toContain('run%2Fa%20b');
	});
});
