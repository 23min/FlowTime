import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { workbench } from './workbench.svelte.js';
import { METRIC_DEFS, DEFAULT_METRIC } from '$lib/utils/metric-defs.js';
import {
	ViewStateStore,
	VIEW_STATE_KEYS,
	resolveDefaultStorage,
} from './view-state.svelte.js';

/**
 * Shared view-state store — m-E21-06 AC13 + AC15.
 *
 * Canonical store for cross-view state shared between topology and heatmap:
 *   - selectedMetric (proxied to workbench)
 *   - activeClasses, currentBin, binCount, playing
 *   - pinnedNodes / pinnedEdges (proxied getters to workbench)
 *   - activeView, sortMode
 *   - rowStabilityOn  (persisted: ft.heatmap.rowStability)
 *   - nodeMode        (persisted: ft.view.nodeMode)
 *
 * Persistence: rowStabilityOn and nodeMode round-trip via localStorage; other fields are
 * session-ephemeral. The store uses an injectable storage adapter so tests can exercise
 * the persistence branches without touching a real browser.
 */

// Simple in-memory localStorage stub for tests.
function makeStorageStub(): Storage {
	const map = new Map<string, string>();
	return {
		getItem: (k: string) => map.get(k) ?? null,
		setItem: (k: string, v: string) => {
			map.set(k, v);
		},
		removeItem: (k: string) => {
			map.delete(k);
		},
		clear: () => map.clear(),
		key: (i: number) => Array.from(map.keys())[i] ?? null,
		get length() {
			return map.size;
		},
	};
}

describe('ViewStateStore — initial defaults', () => {
	let store: ViewStateStore;

	beforeEach(() => {
		workbench.clear();
		store = new ViewStateStore({ storage: makeStorageStub() });
	});

	it('activeView starts as topology', () => {
		expect(store.activeView).toBe('topology');
	});

	it('activeClasses starts empty', () => {
		expect(store.activeClasses.size).toBe(0);
	});

	it('currentBin / binCount start at 0', () => {
		expect(store.currentBin).toBe(0);
		expect(store.binCount).toBe(0);
	});

	it('playing starts false', () => {
		expect(store.playing).toBe(false);
	});

	it('sortMode defaults to topological', () => {
		expect(store.sortMode).toBe('topological');
	});

	it('rowStabilityOn defaults to false', () => {
		expect(store.rowStabilityOn).toBe(false);
	});

	it('nodeMode defaults to operational', () => {
		expect(store.nodeMode).toBe('operational');
	});

	it('selectedMetric proxies to workbench store', () => {
		expect(store.selectedMetric).toBe(DEFAULT_METRIC);
	});

	it('pinnedNodes proxies to workbench.pinned', () => {
		workbench.pin('a', 'service');
		expect(store.pinnedNodes.map((n) => n.id)).toEqual(['a']);
	});

	it('pinnedEdges proxies to workbench.pinnedEdges', () => {
		workbench.pinEdge('a', 'b');
		expect(store.pinnedEdges.map((e) => `${e.from}→${e.to}`)).toEqual(['a→b']);
	});
});

describe('ViewStateStore — activeView', () => {
	let store: ViewStateStore;
	beforeEach(() => {
		workbench.clear();
		store = new ViewStateStore({ storage: makeStorageStub() });
	});

	it('setView changes the active view', () => {
		store.setView('heatmap');
		expect(store.activeView).toBe('heatmap');
	});

	it('setView is idempotent', () => {
		store.setView('heatmap');
		store.setView('heatmap');
		expect(store.activeView).toBe('heatmap');
	});

	it('setView back to topology restores the initial state', () => {
		store.setView('heatmap');
		store.setView('topology');
		expect(store.activeView).toBe('topology');
	});
});

describe('ViewStateStore — activeClasses (add / remove / clear / toggle)', () => {
	let store: ViewStateStore;
	beforeEach(() => {
		workbench.clear();
		store = new ViewStateStore({ storage: makeStorageStub() });
	});

	it('addClass adds a class', () => {
		store.addClass('web');
		expect(store.activeClasses.has('web')).toBe(true);
	});

	it('addClass is idempotent', () => {
		store.addClass('web');
		store.addClass('web');
		expect(store.activeClasses.size).toBe(1);
	});

	it('removeClass removes a class', () => {
		store.addClass('web');
		store.addClass('batch');
		store.removeClass('web');
		expect(Array.from(store.activeClasses)).toEqual(['batch']);
	});

	it('removeClass of an absent class is a no-op', () => {
		store.addClass('web');
		store.removeClass('ghost');
		expect(store.activeClasses.has('web')).toBe(true);
		expect(store.activeClasses.size).toBe(1);
	});

	it('toggleClass adds when absent', () => {
		store.toggleClass('web');
		expect(store.activeClasses.has('web')).toBe(true);
	});

	it('toggleClass removes when present', () => {
		store.addClass('web');
		store.toggleClass('web');
		expect(store.activeClasses.has('web')).toBe(false);
	});

	it('clearClasses drops everything', () => {
		store.addClass('web');
		store.addClass('batch');
		store.clearClasses();
		expect(store.activeClasses.size).toBe(0);
	});

	it('clearClasses on an already-empty set is a no-op', () => {
		store.clearClasses();
		expect(store.activeClasses.size).toBe(0);
	});

	it('setActiveClasses replaces the whole set', () => {
		store.addClass('web');
		store.setActiveClasses(new Set(['batch', 'stream']));
		expect(Array.from(store.activeClasses).sort()).toEqual(['batch', 'stream']);
	});
});

describe('ViewStateStore — scrubber (currentBin / binCount / playing)', () => {
	let store: ViewStateStore;
	beforeEach(() => {
		workbench.clear();
		store = new ViewStateStore({ storage: makeStorageStub() });
	});

	it('setBinCount updates binCount', () => {
		store.setBinCount(24);
		expect(store.binCount).toBe(24);
	});

	it('setCurrentBin updates currentBin', () => {
		store.setBinCount(24);
		store.setCurrentBin(7);
		expect(store.currentBin).toBe(7);
	});

	it('setCurrentBin clamps to [0, binCount - 1]', () => {
		store.setBinCount(10);
		store.setCurrentBin(-1);
		expect(store.currentBin).toBe(0);
		store.setCurrentBin(99);
		expect(store.currentBin).toBe(9);
	});

	it('setCurrentBin is a no-op when binCount is 0 (leaves currentBin at 0)', () => {
		store.setBinCount(0);
		store.setCurrentBin(5);
		expect(store.currentBin).toBe(0);
	});

	it('setPlaying updates playing', () => {
		store.setPlaying(true);
		expect(store.playing).toBe(true);
		store.setPlaying(false);
		expect(store.playing).toBe(false);
	});

	it('setBinCount smaller than currentBin pulls currentBin back into range', () => {
		store.setBinCount(100);
		store.setCurrentBin(50);
		store.setBinCount(10);
		expect(store.currentBin).toBe(9);
	});

	it('setBinCount to 0 collapses currentBin to 0', () => {
		store.setBinCount(5);
		store.setCurrentBin(3);
		store.setBinCount(0);
		expect(store.currentBin).toBe(0);
	});
});

describe('ViewStateStore — sortMode', () => {
	let store: ViewStateStore;
	beforeEach(() => {
		workbench.clear();
		store = new ViewStateStore({ storage: makeStorageStub() });
	});

	it('setSortMode updates the current sort', () => {
		store.setSortMode('max');
		expect(store.sortMode).toBe('max');
		store.setSortMode('mean');
		expect(store.sortMode).toBe('mean');
	});
});

describe('ViewStateStore — fitWidth persistence', () => {
	let storage: Storage;
	beforeEach(() => {
		workbench.clear();
		storage = makeStorageStub();
	});

	it('defaults to false', () => {
		const store = new ViewStateStore({ storage });
		expect(store.fitWidth).toBe(false);
	});

	it('setFitWidth updates and persists', () => {
		const store = new ViewStateStore({ storage });
		store.setFitWidth(true);
		expect(store.fitWidth).toBe(true);
		expect(storage.getItem('ft.heatmap.fitWidth')).toBe('true');
	});

	it('hydrates from storage', () => {
		storage.setItem('ft.heatmap.fitWidth', 'true');
		const store = new ViewStateStore({ storage });
		expect(store.fitWidth).toBe(true);
	});

	it('malformed storage value falls back to false', () => {
		storage.setItem('ft.heatmap.fitWidth', 'nonsense');
		const store = new ViewStateStore({ storage });
		expect(store.fitWidth).toBe(false);
	});
});

describe('ViewStateStore — selectedCell', () => {
	let store: ViewStateStore;
	beforeEach(() => {
		workbench.clear();
		store = new ViewStateStore({ storage: makeStorageStub() });
	});

	it('defaults to null', () => {
		expect(store.selectedCell).toBeNull();
	});

	it('setSelectedCell records nodeId + bin', () => {
		store.setSelectedCell('node-a', 5);
		expect(store.selectedCell).toEqual({ nodeId: 'node-a', bin: 5 });
	});

	it('setSelectedCell replaces the prior selection', () => {
		store.setSelectedCell('node-a', 5);
		store.setSelectedCell('node-b', 12);
		expect(store.selectedCell).toEqual({ nodeId: 'node-b', bin: 12 });
	});

	it('clearSelectedCell resets to null', () => {
		store.setSelectedCell('node-a', 5);
		store.clearSelectedCell();
		expect(store.selectedCell).toBeNull();
	});

	it('clearSelectedCell on an already-empty selection is a no-op', () => {
		store.clearSelectedCell();
		expect(store.selectedCell).toBeNull();
	});
});

describe('ViewStateStore — selectedEdge (m-E21-08 AC3)', () => {
	let store: ViewStateStore;
	beforeEach(() => {
		workbench.clear();
		store = new ViewStateStore({ storage: makeStorageStub() });
	});

	it('defaults to null', () => {
		expect(store.selectedEdge).toBeNull();
	});

	it('setSelectedEdge records the from/to pair', () => {
		store.setSelectedEdge('A', 'B');
		expect(store.selectedEdge).toEqual({ from: 'A', to: 'B' });
	});

	it('setSelectedEdge replaces the prior selection', () => {
		store.setSelectedEdge('A', 'B');
		store.setSelectedEdge('C', 'D');
		expect(store.selectedEdge).toEqual({ from: 'C', to: 'D' });
	});

	it('clearSelectedEdge resets to null', () => {
		store.setSelectedEdge('A', 'B');
		store.clearSelectedEdge();
		expect(store.selectedEdge).toBeNull();
	});

	it('clearSelectedEdge on an already-empty selection is a no-op', () => {
		store.clearSelectedEdge();
		expect(store.selectedEdge).toBeNull();
	});

	it('setSelectedEdge with the same pair is idempotent', () => {
		store.setSelectedEdge('A', 'B');
		store.setSelectedEdge('A', 'B');
		expect(store.selectedEdge).toEqual({ from: 'A', to: 'B' });
	});

	it('selectedEdge is independent of pinnedEdges (selection without pinning)', () => {
		// AC3 contract: a selected edge is not necessarily pinned, and a pinned
		// edge is not necessarily selected. The store records both states
		// independently.
		store.setSelectedEdge('A', 'B');
		expect(store.selectedEdge).toEqual({ from: 'A', to: 'B' });
		expect(store.pinnedEdges).toHaveLength(0);
	});
});

describe('ViewStateStore — rowStabilityOn persistence', () => {
	let storage: Storage;

	beforeEach(() => {
		workbench.clear();
		storage = makeStorageStub();
	});

	it('setRowStabilityOn updates the flag', () => {
		const store = new ViewStateStore({ storage });
		store.setRowStabilityOn(true);
		expect(store.rowStabilityOn).toBe(true);
	});

	it('setRowStabilityOn persists to storage', () => {
		const store = new ViewStateStore({ storage });
		store.setRowStabilityOn(true);
		expect(storage.getItem(VIEW_STATE_KEYS.rowStability)).toBe('true');
	});

	it('a fresh store reads the persisted value on construction', () => {
		storage.setItem(VIEW_STATE_KEYS.rowStability, 'true');
		const store = new ViewStateStore({ storage });
		expect(store.rowStabilityOn).toBe(true);
	});

	it('unrecognized persisted value falls back to false', () => {
		storage.setItem(VIEW_STATE_KEYS.rowStability, 'yes-please');
		const store = new ViewStateStore({ storage });
		expect(store.rowStabilityOn).toBe(false);
	});

	it('toggling off persists false', () => {
		const store = new ViewStateStore({ storage });
		store.setRowStabilityOn(true);
		store.setRowStabilityOn(false);
		expect(storage.getItem(VIEW_STATE_KEYS.rowStability)).toBe('false');
	});
});

describe('ViewStateStore — nodeMode persistence (AC15)', () => {
	let storage: Storage;
	beforeEach(() => {
		workbench.clear();
		storage = makeStorageStub();
	});

	it('setNodeMode updates the mode', () => {
		const store = new ViewStateStore({ storage });
		store.setNodeMode('full');
		expect(store.nodeMode).toBe('full');
		store.setNodeMode('operational');
		expect(store.nodeMode).toBe('operational');
	});

	it('setNodeMode persists to storage', () => {
		const store = new ViewStateStore({ storage });
		store.setNodeMode('full');
		expect(storage.getItem(VIEW_STATE_KEYS.nodeMode)).toBe('full');
	});

	it('a fresh store reads the persisted mode on construction', () => {
		storage.setItem(VIEW_STATE_KEYS.nodeMode, 'full');
		const store = new ViewStateStore({ storage });
		expect(store.nodeMode).toBe('full');
	});

	it('unrecognized persisted value falls back to operational', () => {
		storage.setItem(VIEW_STATE_KEYS.nodeMode, 'wizard-mode');
		const store = new ViewStateStore({ storage });
		expect(store.nodeMode).toBe('operational');
	});
});

describe('ViewStateStore — pin / unpin proxies', () => {
	let store: ViewStateStore;
	beforeEach(() => {
		workbench.clear();
		store = new ViewStateStore({ storage: makeStorageStub() });
	});

	it('pinNode delegates to workbench.pin', () => {
		store.pinNode('a', 'service');
		expect(workbench.isPinned('a')).toBe(true);
	});

	it('unpinNode delegates to workbench.unpin', () => {
		workbench.pin('a');
		store.unpinNode('a');
		expect(workbench.isPinned('a')).toBe(false);
	});

	it('isPinned delegates to workbench.isPinned', () => {
		workbench.pin('a');
		expect(store.isPinned('a')).toBe(true);
		expect(store.isPinned('missing')).toBe(false);
	});

	it('pinnedIds returns the same Set as workbench.selectedIds', () => {
		workbench.pin('a');
		workbench.pin('b');
		expect(Array.from(store.pinnedIds).sort()).toEqual(['a', 'b']);
	});
});

describe('ViewStateStore — selectedMetric proxy', () => {
	let store: ViewStateStore;
	beforeEach(() => {
		workbench.clear();
		store = new ViewStateStore({ storage: makeStorageStub() });
	});

	it('setSelectedMetric delegates to workbench', () => {
		const queue = METRIC_DEFS.find((d) => d.id === 'queue')!;
		store.setSelectedMetric(queue);
		expect(workbench.selectedMetric).toBe(queue);
		expect(store.selectedMetric).toBe(queue);
	});
});

describe('ViewStateStore — storage resolver fallback (no browser)', () => {
	// This branch covers the `options.storage` undefined path: the store falls back to
	// a no-op stub so `.svelte.ts` can import cleanly in node tests. We verify the
	// store constructs without an injected storage adapter and returns defaults.
	it('constructs without an injected storage and returns defaults', () => {
		const store = new ViewStateStore();
		expect(store.rowStabilityOn).toBe(false);
		expect(store.nodeMode).toBe('operational');
		store.setRowStabilityOn(true);
		// Without storage there is no persistence layer to observe, but setRowStabilityOn
		// still updates the in-memory flag.
		expect(store.rowStabilityOn).toBe(true);
	});
});

describe('resolveDefaultStorage — defensive paths', () => {
	it('returns the host\'s localStorage when present', () => {
		const fakeStorage = makeStorageStub();
		const host = { localStorage: fakeStorage };
		expect(resolveDefaultStorage(host)).toBe(fakeStorage);
	});

	it('returns undefined when the host has no localStorage', () => {
		expect(resolveDefaultStorage({})).toBeUndefined();
	});

	it('returns undefined when host is undefined', () => {
		expect(resolveDefaultStorage(undefined)).toBeUndefined();
	});

	it('returns undefined when host is null', () => {
		expect(resolveDefaultStorage(null)).toBeUndefined();
	});

	it('returns undefined when accessing localStorage throws', () => {
		// Host object whose `in` check throws via a proxy — covers the catch path.
		const host = new Proxy(
			{},
			{
				has: () => {
					throw new Error('SecurityError');
				},
			}
		);
		expect(resolveDefaultStorage(host)).toBeUndefined();
	});
});

describe('ViewStateStore — storage that throws (defensive)', () => {
	// If localStorage throws (quota / security error), setters should not crash the
	// component. We stub a storage whose setItem throws and confirm the store falls
	// through.
	const throwingStorage: Storage = {
		getItem: () => null,
		setItem: () => {
			throw new Error('QuotaExceeded');
		},
		removeItem: () => undefined,
		clear: () => undefined,
		key: () => null,
		length: 0,
	};

	let consoleSpy: ReturnType<typeof vi.spyOn>;
	beforeEach(() => {
		workbench.clear();
		consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
	});

	afterEach(() => {
		consoleSpy.mockRestore();
	});

	it('setRowStabilityOn tolerates throwing storage', () => {
		const store = new ViewStateStore({ storage: throwingStorage });
		expect(() => store.setRowStabilityOn(true)).not.toThrow();
		expect(store.rowStabilityOn).toBe(true);
	});

	it('setNodeMode tolerates throwing storage', () => {
		const store = new ViewStateStore({ storage: throwingStorage });
		expect(() => store.setNodeMode('full')).not.toThrow();
		expect(store.nodeMode).toBe('full');
	});

	it('getItem that throws on construction falls back to defaults', () => {
		const getterThrowingStorage: Storage = {
			...throwingStorage,
			getItem: () => {
				throw new Error('SecurityError');
			},
		};
		const store = new ViewStateStore({ storage: getterThrowingStorage });
		expect(store.rowStabilityOn).toBe(false);
		expect(store.nodeMode).toBe('operational');
	});
});
