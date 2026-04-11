import { describe, it, expect, beforeEach, vi } from 'vitest';
import { encode, decode } from '@msgpack/msgpack';
import {
	EngineSession,
	encodeFrame,
	decodeFrame,
	type WebSocketLike,
	type WebSocketFactory,
} from './engine-session';

// readyState constants
const CONNECTING = 0;
const OPEN = 1;
const CLOSED = 3;

// ── Mock WebSocket ──

class MockWebSocket implements WebSocketLike {
	readyState = CONNECTING;
	binaryType = 'arraybuffer';
	onopen: ((ev: unknown) => void) | null = null;
	onmessage: ((ev: { data: unknown }) => void) | null = null;
	onerror: ((ev: unknown) => void) | null = null;
	onclose: ((ev: unknown) => void) | null = null;

	sent: Uint8Array[] = [];

	constructor(public url: string) {}

	send(data: ArrayBuffer | ArrayBufferView): void {
		if (this.readyState !== OPEN) throw new Error('Cannot send: socket not open');
		const bytes = data instanceof ArrayBuffer
			? new Uint8Array(data)
			: new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
		this.sent.push(new Uint8Array(bytes));
	}

	close(_code?: number, _reason?: string): void {
		this.readyState = CLOSED;
		this.onclose?.({});
	}

	// Test helpers
	triggerOpen(): void {
		this.readyState = OPEN;
		this.onopen?.({});
	}

	triggerMessage(response: unknown): void {
		// Encode response with length prefix
		const payload = encode(response);
		const frame = new ArrayBuffer(4 + payload.length);
		const view = new DataView(frame);
		view.setUint32(0, payload.length, false);
		new Uint8Array(frame, 4).set(payload);
		this.onmessage?.({ data: frame });
	}

	triggerError(): void {
		this.onerror?.({});
	}

	triggerClose(): void {
		this.readyState = CLOSED;
		this.onclose?.({});
	}

	/** Decode the most recently sent frame as a request object. */
	lastSentRequest(): { method: string; params: unknown } {
		const frame = this.sent[this.sent.length - 1];
		if (!frame || frame.length < 4) throw new Error('No sent frame');
		const view = new DataView(frame.buffer, frame.byteOffset, frame.byteLength);
		const len = view.getUint32(0, false);
		if (len + 4 !== frame.length) {
			throw new Error(`Frame length mismatch: prefix=${len}, buffer=${frame.length - 4}`);
		}
		return decode(frame.slice(4)) as { method: string; params: unknown };
	}
}

/** Factory that creates mock WebSockets and tracks them. */
function makeFactory(): { factory: WebSocketFactory; sockets: MockWebSocket[] } {
	const sockets: MockWebSocket[] = [];
	const factory: WebSocketFactory = (url) => {
		const ws = new MockWebSocket(url);
		sockets.push(ws);
		return ws;
	};
	return { factory, sockets };
}

/** Yield to the microtask queue so pending async operations can progress. */
async function flush(ticks = 3): Promise<void> {
	for (let i = 0; i < ticks; i++) {
		await Promise.resolve();
	}
}

// ── encodeFrame / decodeFrame unit tests ──

describe('encodeFrame', () => {
	it('produces big-endian length prefix', () => {
		const frame = encodeFrame({ hello: 'world' });
		const view = new DataView(frame.buffer, frame.byteOffset, frame.byteLength);
		const len = view.getUint32(0, false);
		expect(len).toBe(frame.length - 4);
	});

	it('round-trips with decodeFrame', () => {
		const obj = { method: 'compile', params: { yaml: 'test' } };
		const frame = encodeFrame(obj);
		const decoded = decodeFrame(frame.buffer.slice(frame.byteOffset, frame.byteOffset + frame.byteLength) as ArrayBuffer);
		expect(decoded).toEqual(obj);
	});

	it('encodes numeric arrays efficiently', () => {
		const frame = encodeFrame({ data: [1.5, 2.5, 3.5] });
		// Just verify round-trip works
		const decoded = decodeFrame(frame.buffer.slice(frame.byteOffset, frame.byteOffset + frame.byteLength) as ArrayBuffer) as { data: number[] };
		expect(decoded.data).toEqual([1.5, 2.5, 3.5]);
	});
});

describe('decodeFrame', () => {
	it('throws on too-short frame', () => {
		const buf = new ArrayBuffer(3);
		expect(() => decodeFrame(buf)).toThrow(/too short/i);
	});

	it('throws on length prefix mismatch', () => {
		// 4-byte prefix claiming length 10 but only 5 bytes payload
		const buf = new ArrayBuffer(4 + 5);
		const view = new DataView(buf);
		view.setUint32(0, 10, false);
		expect(() => decodeFrame(buf)).toThrow(/length prefix mismatch/i);
	});
});

// ── EngineSession connection lifecycle ──

describe('EngineSession.connect', () => {
	it('opens WebSocket and resolves on open event', async () => {
		const { factory, sockets } = makeFactory();
		const session = new EngineSession('ws://test', factory);

		const connectPromise = session.connect();
		expect(sockets).toHaveLength(1);
		sockets[0].triggerOpen();

		await expect(connectPromise).resolves.toBeUndefined();
	});

	it('is idempotent when already connected', async () => {
		const { factory, sockets } = makeFactory();
		const session = new EngineSession('ws://test', factory);

		const p1 = session.connect();
		sockets[0].triggerOpen();
		await p1;

		await session.connect();
		expect(sockets).toHaveLength(1); // no new socket created
	});

	it('returns same promise for concurrent connect calls', async () => {
		const { factory, sockets } = makeFactory();
		const session = new EngineSession('ws://test', factory);

		const p1 = session.connect();
		const p2 = session.connect();
		expect(sockets).toHaveLength(1); // only one socket

		sockets[0].triggerOpen();
		await Promise.all([p1, p2]);
	});

	it('rejects on WebSocket error', async () => {
		const { factory, sockets } = makeFactory();
		const session = new EngineSession('ws://test', factory);

		const p = session.connect();
		sockets[0].triggerError();
		await expect(p).rejects.toThrow(/WebSocket connection failed/);
	});
});

describe('EngineSession.close', () => {
	it('closes the socket and clears replay state', async () => {
		const { factory, sockets } = makeFactory();
		const session = new EngineSession('ws://test', factory);

		const p = session.connect();
		sockets[0].triggerOpen();
		await p;

		session.close();
		expect(sockets[0].readyState).toBe(CLOSED);
	});

	it('is safe when not connected', () => {
		const { factory } = makeFactory();
		const session = new EngineSession('ws://test', factory);

		expect(() => session.close()).not.toThrow();
	});
});

// ── Command dispatch ──

describe('EngineSession commands', () => {
	let session: EngineSession;
	let sockets: MockWebSocket[];

	beforeEach(async () => {
		const { factory, sockets: s } = makeFactory();
		sockets = s;
		session = new EngineSession('ws://test', factory);
		const p = session.connect();
		sockets[0].triggerOpen();
		await p;
	});

	/** Helper: after calling an async method, flush and respond. */
	async function sendAndRespond<T>(
		promise: Promise<T>,
		response: unknown,
	): Promise<T> {
		await flush();
		sockets[0].triggerMessage(response);
		return promise;
	}

	const basicCompileResponse = {
		result: { params: [], series: {}, bins: 1, grid: { bins: 1, binSize: 1, binUnit: 'h' } },
	};

	it('compile sends request with yaml param and resolves with result', async () => {
		const promise = session.compile('grid:\n  bins: 3');
		await flush();
		const req = sockets[0].lastSentRequest();
		expect(req.method).toBe('compile');
		expect((req.params as { yaml: string }).yaml).toBe('grid:\n  bins: 3');

		sockets[0].triggerMessage({
			result: { params: [], series: {}, bins: 3, grid: { bins: 3, binSize: 1, binUnit: 'hours' } },
		});

		const result = await promise;
		expect(result.bins).toBe(3);
	});

	it('eval sends overrides and resolves with result', async () => {
		await sendAndRespond(session.compile('yaml'), basicCompileResponse);

		const promise = session.eval({ arrivals: 20 });
		await flush();
		const req = sockets[0].lastSentRequest();
		expect(req.method).toBe('eval');
		expect((req.params as { overrides: Record<string, number> }).overrides.arrivals).toBe(20);

		sockets[0].triggerMessage({
			result: { series: { served: [10, 10] }, elapsed_us: 42 },
		});

		const result = await promise;
		expect(result.series.served).toEqual([10, 10]);
		expect(result.elapsed_us).toBe(42);
	});

	it('getParams returns parameter list', async () => {
		await sendAndRespond(session.compile('yaml'), basicCompileResponse);

		const promise = session.getParams();
		await flush();
		sockets[0].triggerMessage({
			result: { params: [{ id: 'x', kind: 'ConstNode', default: 5 }] },
		});

		const result = await promise;
		expect(result.params).toHaveLength(1);
		expect(result.params[0].id).toBe('x');
	});

	it('getSeries with names sends filter', async () => {
		await sendAndRespond(session.compile('yaml'), basicCompileResponse);

		const promise = session.getSeries(['served']);
		await flush();
		const req = sockets[0].lastSentRequest();
		expect((req.params as { names: string[] }).names).toEqual(['served']);

		sockets[0].triggerMessage({ result: { series: { served: [1, 2, 3] } } });
		const result = await promise;
		expect(result.series.served).toEqual([1, 2, 3]);
	});

	it('getSeries with no names sends empty params', async () => {
		await sendAndRespond(session.compile('yaml'), basicCompileResponse);

		const promise = session.getSeries();
		await flush();
		const req = sockets[0].lastSentRequest();
		expect(req.params).toEqual({});

		sockets[0].triggerMessage({ result: { series: { a: [1], b: [2] } } });
		const result = await promise;
		expect(Object.keys(result.series)).toHaveLength(2);
	});
});

// ── Error handling ──

describe('EngineSession error handling', () => {
	let session: EngineSession;
	let sockets: MockWebSocket[];

	beforeEach(async () => {
		const { factory, sockets: s } = makeFactory();
		sockets = s;
		session = new EngineSession('ws://test', factory);
		const p = session.connect();
		sockets[0].triggerOpen();
		await p;
	});

	it('error response rejects the promise with error code', async () => {
		const promise = session.compile('bad yaml');
		await flush();
		sockets[0].triggerMessage({
			error: { code: 'compile_error', message: 'invalid YAML' },
		});

		await expect(promise).rejects.toThrow(/compile_error/);
	});

	it('concurrent calls are rejected', async () => {
		const p1 = session.compile('a');
		await flush();
		// Second call while first is in flight
		await expect(session.compile('b')).rejects.toThrow(/already in flight/);

		sockets[0].triggerMessage({ result: { params: [], series: {}, bins: 1, grid: { bins: 1, binSize: 1, binUnit: 'h' } } });
		await p1;
	});

	it('socket close rejects pending request', async () => {
		const p = session.compile('yaml');
		await flush();
		sockets[0].triggerClose();
		await expect(p).rejects.toThrow(/WebSocket closed/);
	});

	it('non-ArrayBuffer message rejects pending request', async () => {
		const p = session.compile('yaml');
		await flush();
		sockets[0].onmessage?.({ data: 'not an ArrayBuffer' });
		await expect(p).rejects.toThrow(/ArrayBuffer/);
	});

	it('frame too short rejects pending request', async () => {
		const p = session.compile('yaml');
		await flush();
		sockets[0].onmessage?.({ data: new ArrayBuffer(2) });
		await expect(p).rejects.toThrow(/too short/i);
	});

	it('unknown method error is propagated', async () => {
		const p = session.compile('yaml');
		await flush();
		sockets[0].triggerMessage({
			error: { code: 'unknown_method', message: 'Unknown method: bogus' },
		});
		await expect(p).rejects.toMatchObject({ code: 'unknown_method' });
	});
});

// ── Reconnection + replay ──

describe('EngineSession reconnection with replay', () => {
	const compileResponse = {
		result: { params: [{ id: 'x', kind: 'ConstNode', default: 5 }], series: {}, bins: 1, grid: { bins: 1, binSize: 1, binUnit: 'h' } },
	};

	it('replays last compile when socket dropped before eval', async () => {
		const { factory, sockets } = makeFactory();
		const session = new EngineSession('ws://test', factory);

		// Initial connect + compile
		const p1 = session.connect();
		sockets[0].triggerOpen();
		await p1;

		const cp = session.compile('model_yaml');
		await flush();
		sockets[0].triggerMessage(compileResponse);
		await cp;

		// Socket drops
		sockets[0].triggerClose();
		expect(sockets[0].readyState).toBe(CLOSED);

		// Call eval — should open new socket and replay compile first
		const evalPromise = session.eval({ x: 99 });

		await flush();
		expect(sockets).toHaveLength(2);
		sockets[1].triggerOpen();

		// First request on new socket should be replayed compile
		await flush();
		const firstReq = sockets[1].lastSentRequest();
		expect(firstReq.method).toBe('compile');
		expect((firstReq.params as { yaml: string }).yaml).toBe('model_yaml');

		// Respond to compile
		sockets[1].triggerMessage(compileResponse);

		// Then the eval request should be sent
		await flush();
		const secondReq = sockets[1].lastSentRequest();
		expect(secondReq.method).toBe('eval');
		expect((secondReq.params as { overrides: Record<string, number> }).overrides.x).toBe(99);

		sockets[1].triggerMessage({
			result: { series: { out: [99] }, elapsed_us: 5 },
		});

		const result = await evalPromise;
		expect(result.series.out).toEqual([99]);
	});

	it('does not replay if no compile was ever called', async () => {
		const { factory, sockets } = makeFactory();
		const session = new EngineSession('ws://test', factory);

		const p = session.connect();
		sockets[0].triggerOpen();
		await p;
		sockets[0].triggerClose();

		// Call eval with no prior compile — should reconnect but not replay
		const evalPromise = session.eval({ x: 1 });
		await flush();
		expect(sockets).toHaveLength(2);
		sockets[1].triggerOpen();
		await flush();

		const req = sockets[1].lastSentRequest();
		expect(req.method).toBe('eval'); // no compile replayed first

		sockets[1].triggerMessage({
			error: { code: 'not_compiled', message: 'No model compiled' },
		});

		await expect(evalPromise).rejects.toMatchObject({ code: 'not_compiled' });
	});

	it('clears replay state on close()', async () => {
		const { factory, sockets } = makeFactory();
		const session = new EngineSession('ws://test', factory);

		const p = session.connect();
		sockets[0].triggerOpen();
		await p;

		const cp = session.compile('yaml');
		await flush();
		sockets[0].triggerMessage({ result: { params: [], series: {}, bins: 1, grid: { bins: 1, binSize: 1, binUnit: 'h' } } });
		await cp;

		session.close();

		// Next eval should NOT replay compile (state was cleared)
		const evalPromise = session.eval({ x: 1 });
		await flush();
		expect(sockets).toHaveLength(2);
		sockets[1].triggerOpen();
		await flush();

		const req = sockets[1].lastSentRequest();
		expect(req.method).toBe('eval'); // no compile replayed

		sockets[1].triggerMessage({ result: { series: {}, elapsed_us: 1 } });
		await evalPromise;
	});
});
