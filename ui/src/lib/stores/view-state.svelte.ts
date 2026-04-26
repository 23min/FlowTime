/**
 * Shared view-state store — m-E21-06 AC13 + AC15.
 *
 * Single source of truth for state shared between the topology and heatmap views:
 *
 *   - activeView           — 'topology' | 'heatmap'
 *   - activeClasses        — class filter set
 *   - currentBin, binCount, playing
 *   - sortMode             — heatmap row sort mode
 *   - rowStabilityOn       — heatmap "keep filtered rows" toggle (persisted)
 *   - nodeMode             — 'operational' | 'full' (persisted; AC15)
 *   - selectedCell         — heatmap cell-selection marker; also drives workbench-card
 *                            title highlight so the selected tile visually ties to its card
 *   - selectedMetric       — proxied to workbench store
 *   - pinnedNodes, pinnedEdges, isPinned — proxied to workbench store
 *
 * Persistence uses an injectable `Storage` adapter so tests can round-trip through an
 * in-memory stub. Production wiring reads `window.localStorage` on the topology route and
 * passes it in; SSR / headless contexts construct the store with no adapter and the
 * persistence layer no-ops.
 *
 * The workbench store stays authoritative for pin state; the view-state store proxies to
 * it so heatmap-side callers never have to reach across two imports. No breaking changes
 * to `workbench.svelte.ts`'s public surface.
 */

import { workbench, type PinnedNode, type PinnedEdge } from './workbench.svelte.js';
import type { MetricDef } from '$lib/utils/metric-defs.js';
import type { SortMode } from '$lib/utils/heatmap-sort.js';

export type ActiveView = 'topology' | 'heatmap';
export type NodeMode = 'operational' | 'full';

export interface SelectedCell {
	nodeId: string;
	bin: number;
}

export const VIEW_STATE_KEYS = {
	rowStability: 'ft.heatmap.rowStability',
	nodeMode: 'ft.view.nodeMode',
	fitWidth: 'ft.heatmap.fitWidth',
} as const;

export interface ViewStateOptions {
	/**
	 * Storage adapter for persistence. Production callers pass `window.localStorage`;
	 * tests pass an in-memory stub; SSR / node contexts can omit this field and persistence
	 * becomes a no-op.
	 */
	storage?: Storage;
}

/**
 * Defensive read: returns `null` when the storage throws (Safari private mode, SecurityError
 * in cross-origin iframes, etc.) instead of propagating the exception.
 */
function safeGetItem(storage: Storage | undefined, key: string): string | null {
	if (!storage) return null;
	try {
		return storage.getItem(key);
	} catch {
		return null;
	}
}

function safeSetItem(storage: Storage | undefined, key: string, value: string): void {
	if (!storage) return;
	try {
		storage.setItem(key, value);
	} catch (err) {
		console.warn('view-state: failed to persist', { key, err });
	}
}

function parseBoolean(raw: string | null, fallback: boolean): boolean {
	if (raw === 'true') return true;
	if (raw === 'false') return false;
	return fallback;
}

function parseNodeMode(raw: string | null, fallback: NodeMode): NodeMode {
	if (raw === 'operational' || raw === 'full') return raw;
	return fallback;
}

export class ViewStateStore {
	// Rune-backed state.
	activeView = $state<ActiveView>('topology');
	activeClasses = $state<Set<string>>(new Set());
	currentBin = $state<number>(0);
	binCount = $state<number>(0);
	playing = $state<boolean>(false);
	sortMode = $state<SortMode>('topological');
	rowStabilityOn = $state<boolean>(false);
	nodeMode = $state<NodeMode>('operational');
	fitWidth = $state<boolean>(false);
	selectedCell = $state<SelectedCell | null>(null);

	readonly #storage: Storage | undefined;

	constructor(options: ViewStateOptions = {}) {
		this.#storage = options.storage;

		// Hydrate persisted fields. Defensive on both read and parse paths.
		this.rowStabilityOn = parseBoolean(
			safeGetItem(this.#storage, VIEW_STATE_KEYS.rowStability),
			false
		);
		this.nodeMode = parseNodeMode(
			safeGetItem(this.#storage, VIEW_STATE_KEYS.nodeMode),
			'operational'
		);
		this.fitWidth = parseBoolean(
			safeGetItem(this.#storage, VIEW_STATE_KEYS.fitWidth),
			false
		);
	}

	// ---- view switch ----
	setView(v: ActiveView) {
		this.activeView = v;
	}

	// ---- class filter ----
	addClass(cls: string) {
		if (this.activeClasses.has(cls)) return;
		const next = new Set(this.activeClasses);
		next.add(cls);
		this.activeClasses = next;
	}

	removeClass(cls: string) {
		if (!this.activeClasses.has(cls)) return;
		const next = new Set(this.activeClasses);
		next.delete(cls);
		this.activeClasses = next;
	}

	toggleClass(cls: string) {
		if (this.activeClasses.has(cls)) this.removeClass(cls);
		else this.addClass(cls);
	}

	clearClasses() {
		if (this.activeClasses.size === 0) return;
		this.activeClasses = new Set();
	}

	setActiveClasses(classes: ReadonlySet<string>) {
		this.activeClasses = new Set(classes);
	}

	// ---- scrubber ----
	setBinCount(n: number) {
		this.binCount = Math.max(0, Math.floor(n));
		if (this.binCount === 0) {
			this.currentBin = 0;
		} else if (this.currentBin > this.binCount - 1) {
			this.currentBin = this.binCount - 1;
		}
	}

	setCurrentBin(b: number) {
		if (this.binCount === 0) {
			this.currentBin = 0;
			return;
		}
		const max = this.binCount - 1;
		this.currentBin = Math.max(0, Math.min(max, Math.floor(b)));
	}

	setPlaying(p: boolean) {
		this.playing = p;
	}

	// ---- sort ----
	setSortMode(m: SortMode) {
		this.sortMode = m;
	}

	// ---- cell selection (persistent; survives DOM blur) ----
	setSelectedCell(nodeId: string, bin: number) {
		this.selectedCell = { nodeId, bin };
	}

	clearSelectedCell() {
		this.selectedCell = null;
	}

	// ---- row-stability toggle (persisted) ----
	setRowStabilityOn(on: boolean) {
		this.rowStabilityOn = on;
		safeSetItem(this.#storage, VIEW_STATE_KEYS.rowStability, String(on));
	}

	// ---- fit-to-width toggle (persisted) ----
	setFitWidth(on: boolean) {
		this.fitWidth = on;
		safeSetItem(this.#storage, VIEW_STATE_KEYS.fitWidth, String(on));
	}

	// ---- node-mode toggle (persisted; AC15) ----
	setNodeMode(m: NodeMode) {
		this.nodeMode = m;
		safeSetItem(this.#storage, VIEW_STATE_KEYS.nodeMode, m);
	}

	// ---- metric proxy ----
	get selectedMetric(): MetricDef {
		return workbench.selectedMetric;
	}

	setSelectedMetric(m: MetricDef) {
		workbench.selectedMetric = m;
	}

	// ---- pin proxies ----
	get pinnedNodes(): ReadonlyArray<PinnedNode> {
		return workbench.pinned;
	}

	get pinnedEdges(): ReadonlyArray<PinnedEdge> {
		return workbench.pinnedEdges;
	}

	get pinnedIds(): ReadonlySet<string> {
		return workbench.selectedIds;
	}

	pinNode(id: string, kind?: string) {
		workbench.pin(id, kind);
	}

	unpinNode(id: string) {
		workbench.unpin(id);
	}

	isPinned(id: string): boolean {
		return workbench.isPinned(id);
	}
}

/**
 * Resolve the browser's `localStorage` when it exists and is reachable. Returns
 * `undefined` in SSR / node / restricted-iframe contexts so construction of the module-
 * level `viewState` never throws. Exported for targeted coverage of the defensive
 * try/catch path.
 */
export function resolveDefaultStorage(
	host: unknown = typeof globalThis !== 'undefined' ? globalThis : undefined
): Storage | undefined {
	if (host === undefined || host === null) return undefined;
	try {
		const h = host as Record<string, unknown>;
		if ('localStorage' in h) {
			return h.localStorage as Storage;
		}
		return undefined;
	} catch {
		// Accessing localStorage may throw in cross-origin iframe / Safari private mode.
		return undefined;
	}
}

/**
 * Default module-level instance. Wired to `window.localStorage` when the browser APIs
 * exist; otherwise persistence is a no-op. Route and component code imports this instance
 * directly; tests construct their own `ViewStateStore` via the exported class.
 */
export const viewState = new ViewStateStore({ storage: resolveDefaultStorage() });
