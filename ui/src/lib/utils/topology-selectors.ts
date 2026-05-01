/**
 * Pure helpers for topology DOM selectors.
 *
 * Extracted so the escaping and guard-clause behaviour can be unit-tested
 * without a DOM. The consuming $effect in the topology route composes these
 * with querySelectorAll.
 */

export interface EdgeEndpoints {
	from: string;
	to: string;
}

/**
 * Escape a string for use inside a double-quoted CSS attribute-value
 * selector. We only need to escape backslash and double-quote because the
 * value is always wrapped in `"`. Other special CSS characters (`]`, `.`,
 * `#`, numeric-leading ids, etc.) are legal inside a quoted attribute
 * value and need no escaping.
 *
 * This is narrower than `CSS.escape`, which targets identifier-escaping
 * semantics — appropriate for class / id selectors but not needed here.
 * Running in a Node test environment without `CSS` also rules out the
 * global helper.
 */
export function escapeAttributeValue(value: string): string {
	return value.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}

/**
 * Build the attribute selector used to match a pinned edge in the rendered
 * dag-map SVG. Both endpoints are escaped so graph ids containing `"` or
 * `\` do not break out of the attribute-value string and produce a
 * SyntaxError inside querySelectorAll.
 *
 * The `:not([data-edge-hit])` tail filters out the invisible hit-area layer
 * so highlight classes only land on the visible edge.
 */
export function buildEdgeSelector(edge: EdgeEndpoints): string {
	const from = escapeAttributeValue(edge.from);
	const to = escapeAttributeValue(edge.to);
	return `[data-edge-from="${from}"][data-edge-to="${to}"]:not([data-edge-hit])`;
}
