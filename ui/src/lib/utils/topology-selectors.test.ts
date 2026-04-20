import { describe, it, expect } from 'vitest';
import { buildEdgeSelector, escapeAttributeValue } from './topology-selectors.js';

describe('escapeAttributeValue', () => {
	it('passes alphanumeric and dashes through unchanged', () => {
		expect(escapeAttributeValue('node-1_abc')).toBe('node-1_abc');
	});

	it('escapes double-quotes', () => {
		expect(escapeAttributeValue('a"b')).toBe('a\\"b');
	});

	it('escapes backslashes', () => {
		expect(escapeAttributeValue('a\\b')).toBe('a\\\\b');
	});

	it('escapes backslash before escaping quotes to avoid double-escape', () => {
		// The input `\"` must become `\\\"` (backslash → `\\`, then quote → `\"`),
		// not `\\\\"` (which would result if quote escaping ran first and then
		// its own backslash got escaped again).
		expect(escapeAttributeValue('\\"')).toBe('\\\\\\"');
	});

	it('leaves closing-bracket characters unchanged (legal inside quoted value)', () => {
		expect(escapeAttributeValue('a]b')).toBe('a]b');
	});

	it('leaves numeric-leading ids unchanged (legal inside quoted value)', () => {
		expect(escapeAttributeValue('1node')).toBe('1node');
	});

	it('handles empty string', () => {
		expect(escapeAttributeValue('')).toBe('');
	});

	it('handles multiple occurrences', () => {
		expect(escapeAttributeValue('"a"b"')).toBe('\\"a\\"b\\"');
	});
});

describe('buildEdgeSelector', () => {
	it('produces a plain selector for simple ids', () => {
		expect(buildEdgeSelector({ from: 'arrivals', to: 'queue' })).toBe(
			'[data-edge-from="arrivals"][data-edge-to="queue"]:not([data-edge-hit])'
		);
	});

	it('escapes double-quotes in endpoints', () => {
		const selector = buildEdgeSelector({ from: 'a"b', to: 'c' });
		expect(selector).toBe('[data-edge-from="a\\"b"][data-edge-to="c"]:not([data-edge-hit])');
	});

	it('escapes backslashes in endpoints', () => {
		const selector = buildEdgeSelector({ from: 'a\\b', to: 'c' });
		expect(selector).toBe('[data-edge-from="a\\\\b"][data-edge-to="c"]:not([data-edge-hit])');
	});

	it('escapes both endpoints independently', () => {
		const selector = buildEdgeSelector({ from: 'a"b', to: 'c"d' });
		expect(selector).toBe(
			'[data-edge-from="a\\"b"][data-edge-to="c\\"d"]:not([data-edge-hit])'
		);
	});

	it('preserves the non-hit-area filter', () => {
		const selector = buildEdgeSelector({ from: 'x', to: 'y' });
		expect(selector.endsWith(':not([data-edge-hit])')).toBe(true);
	});
});
