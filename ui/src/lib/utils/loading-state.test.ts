import { describe, it, expect } from 'vitest';
import { classifyLoadingState } from './loading-state.js';

describe('classifyLoadingState (m-E21-08 AC5)', () => {
	it('returns "loading" when isLoading is true and there is no result yet', () => {
		expect(classifyLoadingState({ isLoading: true, hasResult: false })).toBe('loading');
	});

	it('returns "loading" when isLoading is true even with a stale result present', () => {
		// Re-running keeps the prior result visible until the new one arrives, but the
		// classifier still returns loading so the surface can paint a skeleton overlay
		// or replace the panel during the new compute.
		expect(classifyLoadingState({ isLoading: true, hasResult: true })).toBe('loading');
	});

	it('returns "result" when isLoading is false and a result exists', () => {
		expect(classifyLoadingState({ isLoading: false, hasResult: true })).toBe('result');
	});

	it('returns "empty" when not loading and no result yet', () => {
		expect(classifyLoadingState({ isLoading: false, hasResult: false })).toBe('empty');
	});
});
