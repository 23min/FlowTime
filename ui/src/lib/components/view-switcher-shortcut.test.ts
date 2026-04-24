import { describe, it, expect } from 'vitest';
import {
	parseShortcut,
	matchViewShortcut,
	type ShortcutEventLike,
	type ViewShortcutDescriptor,
} from './view-switcher-shortcut.js';

describe('parseShortcut', () => {
	it('parses Alt+1', () => {
		expect(parseShortcut('Alt+1')).toEqual({
			alt: true,
			ctrl: false,
			meta: false,
			shift: false,
			key: '1',
		});
	});

	it('parses Ctrl+Alt+h (multiple modifiers, letter key)', () => {
		expect(parseShortcut('Ctrl+Alt+h')).toEqual({
			alt: true,
			ctrl: true,
			meta: false,
			shift: false,
			key: 'h',
		});
	});

	it('accepts Control / Command / Cmd / Meta spellings', () => {
		expect(parseShortcut('Control+x')?.ctrl).toBe(true);
		expect(parseShortcut('Command+x')?.meta).toBe(true);
		expect(parseShortcut('Cmd+x')?.meta).toBe(true);
		expect(parseShortcut('Meta+x')?.meta).toBe(true);
	});

	it('parses Shift+Alt+Tab', () => {
		const parsed = parseShortcut('Shift+Alt+Tab')!;
		expect(parsed.shift).toBe(true);
		expect(parsed.alt).toBe(true);
		expect(parsed.key).toBe('Tab');
	});

	it('returns null for empty descriptor', () => {
		expect(parseShortcut('')).toBeNull();
	});

	it('returns null when trailing key token is empty (e.g. "Alt+")', () => {
		expect(parseShortcut('Alt+')).toBeNull();
	});

	it('unmodified key descriptor works (e.g. "Escape")', () => {
		const parsed = parseShortcut('Escape');
		expect(parsed?.key).toBe('Escape');
		expect(parsed?.alt).toBe(false);
		expect(parsed?.ctrl).toBe(false);
	});
});

function evt(overrides: Partial<ShortcutEventLike> & Pick<ShortcutEventLike, 'key'>): ShortcutEventLike {
	return {
		altKey: false,
		ctrlKey: false,
		metaKey: false,
		shiftKey: false,
		...overrides,
	};
}

describe('matchViewShortcut', () => {
	const views: ViewShortcutDescriptor[] = [
		{ id: 'topology', shortcut: 'Alt+1' },
		{ id: 'heatmap', shortcut: 'Alt+2' },
		{ id: 'no-shortcut' },
	];

	it('Alt+1 → topology', () => {
		expect(matchViewShortcut(evt({ altKey: true, key: '1' }), views)).toBe('topology');
	});

	it('Alt+2 → heatmap', () => {
		expect(matchViewShortcut(evt({ altKey: true, key: '2' }), views)).toBe('heatmap');
	});

	it('Alt+3 → undefined (no view claims it)', () => {
		expect(matchViewShortcut(evt({ altKey: true, key: '3' }), views)).toBeUndefined();
	});

	it('plain "1" (no Alt) → undefined', () => {
		expect(matchViewShortcut(evt({ key: '1' }), views)).toBeUndefined();
	});

	it('Ctrl+Alt+1 does NOT match Alt+1 (exact modifier match)', () => {
		expect(
			matchViewShortcut(evt({ altKey: true, ctrlKey: true, key: '1' }), views)
		).toBeUndefined();
	});

	it('Shift+Alt+1 does NOT match Alt+1', () => {
		expect(
			matchViewShortcut(evt({ altKey: true, shiftKey: true, key: '1' }), views)
		).toBeUndefined();
	});

	it('Cmd+Alt+1 does NOT match Alt+1', () => {
		expect(
			matchViewShortcut(evt({ altKey: true, metaKey: true, key: '1' }), views)
		).toBeUndefined();
	});

	it('views without a shortcut descriptor are skipped silently', () => {
		expect(matchViewShortcut(evt({ altKey: true, key: '3' }), views)).toBeUndefined();
	});

	it('unparseable shortcut descriptors are skipped silently', () => {
		const corrupted: ViewShortcutDescriptor[] = [
			{ id: 'bad', shortcut: 'Alt+' },
			{ id: 'topology', shortcut: 'Alt+1' },
		];
		expect(matchViewShortcut(evt({ altKey: true, key: '1' }), corrupted)).toBe(
			'topology'
		);
	});

	it('empty views list → undefined', () => {
		expect(matchViewShortcut(evt({ altKey: true, key: '1' }), [])).toBeUndefined();
	});
});
