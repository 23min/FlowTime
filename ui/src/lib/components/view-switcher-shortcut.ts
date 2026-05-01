/**
 * Pure-logic helper for the ViewSwitcher component's keyboard shortcut handling.
 *
 * Matches an `Alt+<N>` key combination against a list of views where each view may
 * declare a shortcut like `'Alt+1'`, `'Alt+2'`, etc. Returns the matched view's id, or
 * `undefined` if no view claims the pressed combination.
 *
 * Keeping this as a pure helper lets vitest exercise the matching rules without booting
 * a DOM. The component body only calls this helper and triggers `onChange`.
 */

export interface ShortcutEventLike {
	altKey: boolean;
	ctrlKey?: boolean;
	metaKey?: boolean;
	shiftKey?: boolean;
	/** `KeyboardEvent.key`, e.g. `'1'`, `'h'`, `'Alt'`. */
	key: string;
}

export interface ViewShortcutDescriptor {
	id: string;
	shortcut?: string;
}

/**
 * Normalize a shortcut descriptor string like `'Alt+1'` into its modifier and key parts.
 * Returns null for unrecognized shapes so callers can silently skip them.
 */
export function parseShortcut(descriptor: string): {
	alt: boolean;
	ctrl: boolean;
	meta: boolean;
	shift: boolean;
	key: string;
} | null {
	// `String.prototype.split` always returns at least one element, so parts.length is
	// always >= 1 — we only need to guard against the trailing key being empty (e.g. the
	// descriptor `'Alt+'`). Empty descriptor string → split returns [''], key = '' → null.
	const parts = descriptor.split('+').map((p) => p.trim());
	const key = parts[parts.length - 1];
	if (!key) return null;
	const modifiers = parts.slice(0, -1).map((m) => m.toLowerCase());
	return {
		alt: modifiers.includes('alt'),
		ctrl: modifiers.includes('ctrl') || modifiers.includes('control'),
		meta: modifiers.includes('meta') || modifiers.includes('cmd') || modifiers.includes('command'),
		shift: modifiers.includes('shift'),
		key,
	};
}

/**
 * Given a keyboard event and the view list, return the matching view id — or undefined.
 *
 * Shortcut requires an exact modifier match: `Alt+1` will NOT fire when the user also
 * holds Ctrl. This avoids stealing system/app shortcuts that layer on Alt.
 */
export function matchViewShortcut(
	event: ShortcutEventLike,
	views: ReadonlyArray<ViewShortcutDescriptor>
): string | undefined {
	for (const v of views) {
		if (!v.shortcut) continue;
		const parsed = parseShortcut(v.shortcut);
		if (!parsed) continue;
		if (event.altKey !== parsed.alt) continue;
		if (!!event.ctrlKey !== parsed.ctrl) continue;
		if (!!event.metaKey !== parsed.meta) continue;
		if (!!event.shiftKey !== parsed.shift) continue;
		if (event.key !== parsed.key) continue;
		return v.id;
	}
	return undefined;
}
