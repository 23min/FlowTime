/**
 * Keyboard-nav geometry for the heatmap grid (m-E21-06 AC12).
 *
 * Pure helper that, given the current `(nodeId, bin)` focus, the visible row ids
 * in render order, and the bin count, returns the next target when the user
 * presses an arrow key. Returns `null` for any key that is not a nav key or when
 * the inputs are degenerate (unknown node id, zero bins, zero rows) so callers
 * can safely no-op.
 *
 * The Svelte component owns:
 *   - converting the computed target to a DOM focus() call.
 *   - Enter / Space → pin + scrub (component logic — not a focus target).
 *   - Escape → focus toolbar (component logic — not a grid-local target).
 */

export interface NavInput {
	nodeId: string;
	bin: number;
	rowIds: ReadonlyArray<string>;
	binCount: number;
}

export interface NavTarget {
	nodeId: string;
	bin: number;
}

const ARROW_MOVES: Record<string, [number, number]> = {
	ArrowRight: [0, 1],
	ArrowLeft: [0, -1],
	ArrowDown: [1, 0],
	ArrowUp: [-1, 0],
};

export function computeNextFocus(input: NavInput, key: string): NavTarget | null {
	const move = ARROW_MOVES[key];
	if (!move) return null;
	if (input.binCount <= 0) return null;
	if (input.rowIds.length === 0) return null;
	const rowIdx = input.rowIds.indexOf(input.nodeId);
	if (rowIdx < 0) return null;
	const nextRowIdx = Math.max(0, Math.min(input.rowIds.length - 1, rowIdx + move[0]));
	const nextBin = Math.max(0, Math.min(input.binCount - 1, input.bin + move[1]));
	return { nodeId: input.rowIds[nextRowIdx], bin: nextBin };
}
