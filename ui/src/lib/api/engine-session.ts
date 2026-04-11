// Engine session WebSocket client.
//
// Connects to /v1/engine/session, speaks length-prefixed MessagePack,
// and exposes typed async methods for compile/eval/get_params/get_series.
//
// Handles reconnection: if the WebSocket drops mid-session, the client
// transparently re-opens and replays the last compile + overrides on the
// next call, so the caller never sees a "not_compiled" error from a
// dropped connection.

import { encode, decode } from '@msgpack/msgpack';

export interface ParamInfo {
	id: string;
	kind: string;
	default: number | number[];
}

export interface GridInfo {
	bins: number;
	binSize: number;
	binUnit: string;
}

export interface CompileResult {
	params: ParamInfo[];
	series: Record<string, number[]>;
	bins: number;
	grid: GridInfo;
}

export interface EvalResult {
	series: Record<string, number[]>;
	elapsed_us: number;
}

export interface EngineError {
	code: string;
	message: string;
}

export type ParamOverride = number | number[];

type PendingResolver = {
	resolve: (value: unknown) => void;
	reject: (reason: unknown) => void;
};

/**
 * Minimal WebSocket-like interface so we can inject a mock in tests.
 */
export interface WebSocketLike {
	readyState: number;
	binaryType: string;
	onopen: ((ev: unknown) => void) | null;
	onmessage: ((ev: { data: unknown }) => void) | null;
	onerror: ((ev: unknown) => void) | null;
	onclose: ((ev: unknown) => void) | null;
	send(data: ArrayBuffer | ArrayBufferView): void;
	close(code?: number, reason?: string): void;
}

export type WebSocketFactory = (url: string) => WebSocketLike;

const defaultFactory: WebSocketFactory = (url) => {
	const ws = new WebSocket(url);
	ws.binaryType = 'arraybuffer';
	return ws as unknown as WebSocketLike;
};

// readyState constants (match browser WebSocket)
const WS_CONNECTING = 0;
const WS_OPEN = 1;

/**
 * A persistent WebSocket connection to the engine session bridge.
 *
 * Commands are sequential — only one request is in flight at a time.
 * If the WebSocket drops and a new call is made, the client automatically
 * reconnects and replays the last compile (if any) before the new request.
 */
export class EngineSession {
	private ws: WebSocketLike | null = null;
	private pending: PendingResolver | null = null;
	private connectPromise: Promise<void> | null = null;

	// Replay state: last compile YAML, last overrides applied
	private lastYaml: string | null = null;
	private lastOverrides: Record<string, ParamOverride> | null = null;

	constructor(
		private readonly url: string,
		private readonly factory: WebSocketFactory = defaultFactory,
	) {}

	/**
	 * Open the WebSocket connection. Safe to call multiple times.
	 */
	async connect(): Promise<void> {
		if (this.ws && this.ws.readyState === WS_OPEN) return;
		if (this.connectPromise) return this.connectPromise;

		this.connectPromise = new Promise<void>((resolve, reject) => {
			const ws = this.factory(this.url);
			ws.binaryType = 'arraybuffer';

			ws.onopen = () => {
				this.ws = ws;
				this.connectPromise = null;
				resolve();
			};

			ws.onmessage = (event: { data: unknown }) => {
				this.handleMessage(event.data);
			};

			ws.onerror = () => {
				this.connectPromise = null;
				reject(new Error('WebSocket connection failed'));
			};

			ws.onclose = () => {
				this.ws = null;
				this.connectPromise = null;
				if (this.pending) {
					this.pending.reject(new Error('WebSocket closed'));
					this.pending = null;
				}
			};
		});

		return this.connectPromise;
	}

	/**
	 * Close the WebSocket connection and clear replay state.
	 */
	close(): void {
		if (this.ws && this.ws.readyState === WS_OPEN) {
			this.ws.close(1000, 'client_closed');
		}
		this.ws = null;
		this.lastYaml = null;
		this.lastOverrides = null;
	}

	/**
	 * Compile a model. Returns parameter schema and initial series.
	 * The YAML is cached for automatic replay on reconnect.
	 */
	async compile(yaml: string): Promise<CompileResult> {
		const result = (await this.rawCall('compile', { yaml })) as CompileResult;
		this.lastYaml = yaml;
		this.lastOverrides = null;
		return result;
	}

	/**
	 * Evaluate with parameter overrides. Returns updated series.
	 * The overrides are cached for automatic replay on reconnect.
	 */
	async eval(overrides: Record<string, ParamOverride>): Promise<EvalResult> {
		const result = (await this.callWithReplay('eval', { overrides })) as EvalResult;
		this.lastOverrides = { ...overrides };
		return result;
	}

	/**
	 * Get the current parameter schema.
	 */
	async getParams(): Promise<{ params: ParamInfo[] }> {
		return (await this.callWithReplay('get_params', {})) as { params: ParamInfo[] };
	}

	/**
	 * Get series by name. If names is omitted, returns all non-internal series.
	 */
	async getSeries(names?: string[]): Promise<{ series: Record<string, number[]> }> {
		const params = names ? { names } : {};
		return (await this.callWithReplay('get_series', params)) as {
			series: Record<string, number[]>;
		};
	}

	/**
	 * Call a method that requires a compiled model. If the WebSocket has
	 * dropped since the last compile, reconnect and replay the compile first.
	 */
	private async callWithReplay(method: string, params: unknown): Promise<unknown> {
		// If we have a cached compile and the socket is closed, replay it
		const needsReplay =
			this.lastYaml !== null && (!this.ws || this.ws.readyState !== WS_OPEN);

		if (needsReplay) {
			await this.rawCall('compile', { yaml: this.lastYaml });
			// Re-apply last overrides if any (best-effort)
			if (this.lastOverrides && Object.keys(this.lastOverrides).length > 0 && method !== 'eval') {
				await this.rawCall('eval', { overrides: this.lastOverrides });
			}
		}

		return this.rawCall(method, params);
	}

	/**
	 * Send a raw request and wait for the response. Sequential — one in-flight.
	 */
	private async rawCall(method: string, params: unknown): Promise<unknown> {
		await this.connect();
		if (!this.ws || this.ws.readyState !== WS_OPEN) {
			throw new Error('WebSocket not connected');
		}
		if (this.pending) {
			throw new Error('Request already in flight — session is sequential');
		}

		const request = { method, params };
		const frame = encodeFrame(request);

		return new Promise((resolve, reject) => {
			this.pending = { resolve, reject };
			try {
				this.ws!.send(frame);
			} catch (err) {
				this.pending = null;
				reject(err);
			}
		});
	}

	private handleMessage(data: unknown): void {
		if (!this.pending) return; // unsolicited message — ignore

		try {
			if (!(data instanceof ArrayBuffer)) {
				this.pending.reject(new Error('Expected ArrayBuffer message'));
				this.pending = null;
				return;
			}

			// Strip 4-byte length prefix
			if (data.byteLength < 4) {
				this.pending.reject(new Error('Frame too short'));
				this.pending = null;
				return;
			}
			const payload = new Uint8Array(data, 4);
			const response = decode(payload) as { result?: unknown; error?: EngineError };

			const pending = this.pending;
			this.pending = null;

			if (response.error) {
				const err = new Error(`${response.error.code}: ${response.error.message}`);
				// Attach code as a property without overwriting Error.message
				(err as Error & { code: string }).code = response.error.code;
				pending.reject(err);
			} else {
				pending.resolve(response.result);
			}
		} catch (err) {
			this.pending?.reject(err);
			this.pending = null;
		}
	}
}

/**
 * Encode a request as length-prefixed MessagePack: [4-byte BE length][payload].
 */
export function encodeFrame(obj: unknown): Uint8Array {
	const payload = encode(obj);
	const frame = new Uint8Array(4 + payload.length);
	const view = new DataView(frame.buffer);
	view.setUint32(0, payload.length, false); // big-endian
	frame.set(payload, 4);
	return frame;
}

/**
 * Decode a length-prefixed MessagePack frame. Returns null if the prefix
 * doesn't match the buffer length.
 */
export function decodeFrame(frame: ArrayBuffer): unknown {
	if (frame.byteLength < 4) throw new Error('Frame too short');
	const view = new DataView(frame);
	const len = view.getUint32(0, false);
	if (len + 4 !== frame.byteLength) {
		throw new Error(`Length prefix mismatch: prefix=${len}, buffer=${frame.byteLength - 4}`);
	}
	return decode(new Uint8Array(frame, 4));
}
