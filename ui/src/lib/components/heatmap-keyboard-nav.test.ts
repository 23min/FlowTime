import { describe, it, expect } from 'vitest';
import { computeNextFocus, type NavInput } from './heatmap-keyboard-nav.js';

/**
 * Keyboard nav pure-helper for the heatmap grid (m-E21-06 AC12).
 *
 * Given the current cell position, the visible row ids (in render order), the
 * bin count, and a keypress, returns the next (rowId, bin) focus target — or
 * null when the key is not a nav key. The Svelte component uses this to decide
 * which DOM node to focus after the keypress.
 */

const ROWS: ReadonlyArray<string> = ['a', 'b', 'c'];
const BIN_COUNT = 4;

function at(nodeId: string, bin: number): NavInput {
	return { nodeId, bin, rowIds: ROWS, binCount: BIN_COUNT };
}

describe('computeNextFocus — arrow keys', () => {
	it('ArrowRight advances bin', () => {
		expect(computeNextFocus(at('a', 0), 'ArrowRight')).toEqual({ nodeId: 'a', bin: 1 });
	});

	it('ArrowRight clamps at bin max', () => {
		expect(computeNextFocus(at('a', BIN_COUNT - 1), 'ArrowRight')).toEqual({
			nodeId: 'a',
			bin: BIN_COUNT - 1,
		});
	});

	it('ArrowLeft decrements bin', () => {
		expect(computeNextFocus(at('a', 2), 'ArrowLeft')).toEqual({ nodeId: 'a', bin: 1 });
	});

	it('ArrowLeft clamps at 0', () => {
		expect(computeNextFocus(at('a', 0), 'ArrowLeft')).toEqual({ nodeId: 'a', bin: 0 });
	});

	it('ArrowDown advances row', () => {
		expect(computeNextFocus(at('a', 2), 'ArrowDown')).toEqual({ nodeId: 'b', bin: 2 });
	});

	it('ArrowDown clamps at last row', () => {
		expect(computeNextFocus(at('c', 2), 'ArrowDown')).toEqual({ nodeId: 'c', bin: 2 });
	});

	it('ArrowUp retreats row', () => {
		expect(computeNextFocus(at('b', 2), 'ArrowUp')).toEqual({ nodeId: 'a', bin: 2 });
	});

	it('ArrowUp clamps at first row', () => {
		expect(computeNextFocus(at('a', 2), 'ArrowUp')).toEqual({ nodeId: 'a', bin: 2 });
	});
});

describe('computeNextFocus — non-nav keys return null', () => {
	it('returns null for Enter', () => {
		expect(computeNextFocus(at('a', 0), 'Enter')).toBeNull();
	});

	it('returns null for alphabetic', () => {
		expect(computeNextFocus(at('a', 0), 'x')).toBeNull();
	});

	it('returns null for Tab', () => {
		expect(computeNextFocus(at('a', 0), 'Tab')).toBeNull();
	});
});

describe('computeNextFocus — edge cases', () => {
	it('returns null when current nodeId is not in the row list', () => {
		expect(computeNextFocus({ nodeId: 'missing', bin: 0, rowIds: ROWS, binCount: BIN_COUNT }, 'ArrowDown')).toBeNull();
	});

	it('returns null when binCount <= 0', () => {
		expect(computeNextFocus({ nodeId: 'a', bin: 0, rowIds: ROWS, binCount: 0 }, 'ArrowRight')).toBeNull();
	});

	it('returns null when rowIds is empty', () => {
		expect(computeNextFocus({ nodeId: 'a', bin: 0, rowIds: [], binCount: BIN_COUNT }, 'ArrowDown')).toBeNull();
	});
});
