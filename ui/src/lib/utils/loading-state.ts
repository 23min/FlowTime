/**
 * Pure loading-state classifier — m-E21-08 AC5.
 *
 * Each surface that fetches data tracks two booleans: an `isLoading` flag
 * (request in flight) and a `hasResult` flag (a previous result is in
 * memory). The classifier collapses those into a single render mode the
 * Svelte template can switch on:
 *
 *   - `loading` — show a skeleton placeholder. Wins over `result` so a
 *     re-run replaces the visible content rather than leaving the prior
 *     result alongside a spinner-style indicator.
 *   - `result`  — show the populated content.
 *   - `empty`   — surface-specific empty state ("no run yet", "configure
 *     and click Run").
 *
 * Kept free of any UI types so vitest can run in `node` environment.
 */
export type LoadingState = 'loading' | 'result' | 'empty';

export interface LoadingStateInput {
	isLoading: boolean;
	hasResult: boolean;
}

export function classifyLoadingState(input: LoadingStateInput): LoadingState {
	if (input.isLoading) return 'loading';
	if (input.hasResult) return 'result';
	return 'empty';
}
