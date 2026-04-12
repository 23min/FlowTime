// Pure helpers for the warnings surface (m-E17-04).
//
// No DOM, no side effects — unit-tested in warnings.test.ts.

import type { WarningInfo } from './engine-session.js';

export interface WarningGroup {
	nodeId: string;
	warnings: WarningInfo[];
}

/**
 * Group warnings by node_id, preserving insertion order within each group
 * and first-seen order of node_ids.
 *
 * Example:
 *   [{node_id: A, ...}, {node_id: B, ...}, {node_id: A, ...}]
 *   → [{ nodeId: A, warnings: [0, 2] }, { nodeId: B, warnings: [1] }]
 */
export function groupWarningsByNode(warnings: WarningInfo[]): WarningGroup[] {
	const index = new Map<string, WarningGroup>();
	const order: string[] = [];

	for (const w of warnings) {
		let group = index.get(w.node_id);
		if (!group) {
			group = { nodeId: w.node_id, warnings: [] };
			index.set(w.node_id, group);
			order.push(w.node_id);
		}
		group.warnings.push(w);
	}

	return order.map((id) => index.get(id)!);
}

/**
 * True if any warning in the array references the given node id.
 */
export function nodeHasWarning(warnings: WarningInfo[], nodeId: string): boolean {
	for (const w of warnings) {
		if (w.node_id === nodeId) return true;
	}
	return false;
}

/**
 * Set of node ids that have at least one warning.
 * Useful when rendering a batch of nodes and wanting O(1) lookups.
 */
export function nodesWithWarnings(warnings: WarningInfo[]): Set<string> {
	const set = new Set<string>();
	for (const w of warnings) {
		set.add(w.node_id);
	}
	return set;
}

/**
 * Classify a severity string into one of our supported UI severities.
 * Falls back to 'warning' for unknown values — never throws.
 */
export function severityClass(severity: string): 'warning' | 'error' | 'info' {
	const s = severity.toLowerCase();
	if (s === 'error' || s === 'critical' || s === 'fatal') return 'error';
	if (s === 'info' || s === 'note') return 'info';
	return 'warning';
}

/**
 * Short summary line for a warning, suitable for the banner:
 *   "Service · served_exceeds_capacity"
 */
export function warningSummary(w: WarningInfo): string {
	return `${w.node_id} · ${w.code}`;
}

/**
 * Build a compact "N warnings" banner title.
 *   0 warnings → null (caller should hide the banner)
 *   1 warning  → "1 warning"
 *   N warnings → "N warnings"
 */
export function warningBannerTitle(warnings: WarningInfo[]): string | null {
	if (warnings.length === 0) return null;
	if (warnings.length === 1) return '1 warning';
	return `${warnings.length} warnings`;
}
