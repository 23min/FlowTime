import { describe, it, expect } from 'vitest';
import {
	groupWarningsByNode,
	nodeHasWarning,
	nodesWithWarnings,
	severityClass,
	warningSummary,
	warningBannerTitle,
} from './warnings';
import type { WarningInfo } from './engine-session';

function warning(node_id: string, code: string, extra: Partial<WarningInfo> = {}): WarningInfo {
	return {
		node_id,
		code,
		message: `${code} on ${node_id}`,
		bins: [0, 1, 2],
		severity: 'warning',
		...extra,
	};
}

// ── groupWarningsByNode ──

describe('groupWarningsByNode', () => {
	it('returns empty array for empty input', () => {
		expect(groupWarningsByNode([])).toEqual([]);
	});

	it('groups a single warning', () => {
		const groups = groupWarningsByNode([warning('A', 'x')]);
		expect(groups).toHaveLength(1);
		expect(groups[0].nodeId).toBe('A');
		expect(groups[0].warnings).toHaveLength(1);
	});

	it('groups multiple warnings on the same node', () => {
		const groups = groupWarningsByNode([
			warning('A', 'x'),
			warning('A', 'y'),
		]);
		expect(groups).toHaveLength(1);
		expect(groups[0].warnings.map((w) => w.code)).toEqual(['x', 'y']);
	});

	it('preserves first-seen order of node ids', () => {
		const groups = groupWarningsByNode([
			warning('B', 'x'),
			warning('A', 'x'),
			warning('C', 'x'),
		]);
		expect(groups.map((g) => g.nodeId)).toEqual(['B', 'A', 'C']);
	});

	it('handles interleaved warnings correctly', () => {
		const groups = groupWarningsByNode([
			warning('A', 'x'),
			warning('B', 'x'),
			warning('A', 'y'),
			warning('B', 'y'),
		]);
		expect(groups.map((g) => g.nodeId)).toEqual(['A', 'B']);
		expect(groups[0].warnings.map((w) => w.code)).toEqual(['x', 'y']);
		expect(groups[1].warnings.map((w) => w.code)).toEqual(['x', 'y']);
	});
});

// ── nodeHasWarning ──

describe('nodeHasWarning', () => {
	it('false for empty warnings', () => {
		expect(nodeHasWarning([], 'A')).toBe(false);
	});

	it('true when a warning matches the node', () => {
		expect(nodeHasWarning([warning('A', 'x')], 'A')).toBe(true);
	});

	it('false for a different node', () => {
		expect(nodeHasWarning([warning('A', 'x')], 'B')).toBe(false);
	});

	it('is case-sensitive', () => {
		expect(nodeHasWarning([warning('Service', 'x')], 'service')).toBe(false);
	});
});

// ── nodesWithWarnings ──

describe('nodesWithWarnings', () => {
	it('returns empty set for empty input', () => {
		expect(nodesWithWarnings([]).size).toBe(0);
	});

	it('returns unique node ids', () => {
		const set = nodesWithWarnings([
			warning('A', 'x'),
			warning('A', 'y'),
			warning('B', 'x'),
		]);
		expect(set.size).toBe(2);
		expect(set.has('A')).toBe(true);
		expect(set.has('B')).toBe(true);
	});
});

// ── severityClass ──

describe('severityClass', () => {
	it('maps warning severities', () => {
		expect(severityClass('warning')).toBe('warning');
		expect(severityClass('Warning')).toBe('warning');
		expect(severityClass('WARNING')).toBe('warning');
	});

	it('maps error severities', () => {
		expect(severityClass('error')).toBe('error');
		expect(severityClass('critical')).toBe('error');
		expect(severityClass('fatal')).toBe('error');
	});

	it('maps info severities', () => {
		expect(severityClass('info')).toBe('info');
		expect(severityClass('note')).toBe('info');
	});

	it('falls back to warning for unknown severity', () => {
		expect(severityClass('unknown')).toBe('warning');
		expect(severityClass('')).toBe('warning');
	});
});

// ── warningSummary ──

describe('warningSummary', () => {
	it('formats as "node · code"', () => {
		expect(warningSummary(warning('Service', 'served_exceeds_capacity'))).toBe(
			'Service · served_exceeds_capacity',
		);
	});
});

// ── warningBannerTitle ──

describe('warningBannerTitle', () => {
	it('returns null when there are no warnings', () => {
		expect(warningBannerTitle([])).toBeNull();
	});

	it('returns singular for 1 warning', () => {
		expect(warningBannerTitle([warning('A', 'x')])).toBe('1 warning');
	});

	it('returns plural for multiple warnings', () => {
		expect(warningBannerTitle([warning('A', 'x'), warning('B', 'y')])).toBe('2 warnings');
	});
});
