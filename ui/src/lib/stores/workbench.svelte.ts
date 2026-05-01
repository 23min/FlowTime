/**
 * Workbench store — manages pinned nodes and edges for inspection.
 * Session-ephemeral (not persisted to localStorage).
 */

import { DEFAULT_METRIC, type MetricDef } from '$lib/utils/metric-defs.js';

export interface PinnedNode {
	id: string;
	kind?: string;
}

export interface PinnedEdge {
	from: string;
	to: string;
}

class WorkbenchStore {
	pinned = $state<PinnedNode[]>([]);
	pinnedEdges = $state<PinnedEdge[]>([]);
	selectedMetric = $state<MetricDef>(DEFAULT_METRIC);

	get selectedIds(): Set<string> {
		return new Set(this.pinned.map((n) => n.id));
	}

	get selectedEdgeKeys(): Set<string> {
		return new Set(this.pinnedEdges.map((e) => `${e.from}\u2192${e.to}`));
	}

	pin(id: string, kind?: string) {
		if (!this.pinned.some((n) => n.id === id)) {
			this.pinned = [...this.pinned, { id, kind }];
		}
	}

	unpin(id: string) {
		this.pinned = this.pinned.filter((n) => n.id !== id);
	}

	toggle(id: string, kind?: string) {
		if (this.pinned.some((n) => n.id === id)) {
			this.unpin(id);
		} else {
			this.pin(id, kind);
		}
	}

	pinEdge(from: string, to: string) {
		const key = `${from}\u2192${to}`;
		if (!this.pinnedEdges.some((e) => `${e.from}\u2192${e.to}` === key)) {
			this.pinnedEdges = [...this.pinnedEdges, { from, to }];
		}
	}

	/**
	 * Ensure the edge is pinned AND ordered last in `pinnedEdges`. If the edge
	 * is already pinned, it is moved to the end of the array; if absent, it is
	 * appended. Used by callers (e.g. the validation panel's edge-row click)
	 * that need the cross-link "last-pinned wins" convention to focus on the
	 * just-clicked edge regardless of whether it was pinned previously.
	 *
	 * Atomic \u2014 performs a single `pinnedEdges` reassignment so consumers see
	 * exactly one reactivity tick. Distinct from `pinEdge` (idempotent append-
	 * if-absent) and `toggleEdge` (true toggle); kept as a separate method so
	 * existing callers of those two surfaces are unchanged.
	 */
	bringEdgeToFront(from: string, to: string) {
		const key = `${from}\u2192${to}`;
		const filtered = this.pinnedEdges.filter(
			(e) => `${e.from}\u2192${e.to}` !== key,
		);
		this.pinnedEdges = [...filtered, { from, to }];
	}

	unpinEdge(from: string, to: string) {
		this.pinnedEdges = this.pinnedEdges.filter(
			(e) => !(e.from === from && e.to === to)
		);
	}

	toggleEdge(from: string, to: string) {
		const key = `${from}\u2192${to}`;
		if (this.pinnedEdges.some((e) => `${e.from}\u2192${e.to}` === key)) {
			this.unpinEdge(from, to);
		} else {
			this.pinEdge(from, to);
		}
	}

	isPinned(id: string): boolean {
		return this.pinned.some((n) => n.id === id);
	}

	clear() {
		this.pinned = [];
		this.pinnedEdges = [];
		this.selectedMetric = DEFAULT_METRIC;
	}
}

export const workbench = new WorkbenchStore();
