/**
 * Workbench store — manages pinned nodes for inspection.
 * Session-ephemeral (not persisted to localStorage).
 */

export interface PinnedNode {
	id: string;
	kind?: string;
}

class WorkbenchStore {
	pinned = $state<PinnedNode[]>([]);

	get selectedIds(): Set<string> {
		return new Set(this.pinned.map((n) => n.id));
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

	isPinned(id: string): boolean {
		return this.pinned.some((n) => n.id === id);
	}

	clear() {
		this.pinned = [];
	}
}

export const workbench = new WorkbenchStore();
