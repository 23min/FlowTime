import { browser } from '$app/environment';

export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'ft.theme';

function getSystemPreference(): 'light' | 'dark' {
	if (!browser) return 'light';
	return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

function getStoredMode(): ThemeMode {
	if (!browser) return 'system';
	const stored = localStorage.getItem(STORAGE_KEY);
	if (stored === 'light' || stored === 'dark' || stored === 'system') return stored;
	return 'system';
}

function applyTheme(resolved: 'light' | 'dark') {
	if (!browser) return;
	document.documentElement.classList.toggle('dark', resolved === 'dark');
}

class ThemeStore {
	mode = $state<ThemeMode>(getStoredMode());
	resolved = $derived<'light' | 'dark'>(
		this.mode === 'system' ? getSystemPreference() : this.mode
	);

	#initialized = false;

	init() {
		if (this.#initialized || !browser) return;
		this.#initialized = true;

		$effect(() => {
			applyTheme(this.resolved);
			localStorage.setItem(STORAGE_KEY, this.mode);
		});

		const mq = window.matchMedia('(prefers-color-scheme: dark)');
		mq.addEventListener('change', () => {
			if (this.mode === 'system') {
				applyTheme(getSystemPreference());
			}
		});
	}

	toggle() {
		const order: ThemeMode[] = ['light', 'dark', 'system'];
		const idx = order.indexOf(this.mode);
		this.mode = order[(idx + 1) % order.length];
	}

	set(mode: ThemeMode) {
		this.mode = mode;
	}
}

export const theme = new ThemeStore();
