import { describe, it, expect } from 'vitest';
import { paramControlConfig, kindLabel } from './param-controls';
import type { ParamInfo } from './engine-session';

function scalar(id: string, defaultValue: number, kind = 'ConstNode'): ParamInfo {
	return { id, kind, default: defaultValue };
}

function vector(id: string, values: number[], kind = 'ConstNode'): ParamInfo {
	return { id, kind, default: values };
}

describe('paramControlConfig', () => {
	it('produces a scalar config for positive defaults', () => {
		const config = paramControlConfig(scalar('x', 10));
		expect(config).toEqual({
			type: 'scalar',
			min: 1, // 10 * 0.1
			max: 30, // 10 * 3
			step: 0.1, // 10 / 100
			initial: 10,
		});
	});

	it('uses d + 10 as max when d * 3 < d + 10', () => {
		// d=2 → d*3=6, d+10=12, max should be 12
		const config = paramControlConfig(scalar('x', 2));
		expect((config as { type: 'scalar'; max: number }).max).toBe(12);
	});

	it('uses d * 3 as max when d * 3 > d + 10', () => {
		// d=20 → d*3=60, d+10=30, max should be 60
		const config = paramControlConfig(scalar('x', 20));
		expect((config as { type: 'scalar'; max: number }).max).toBe(60);
	});

	it('enforces min step of 0.1 for small defaults', () => {
		// d=1 → d/100=0.01, but min step is 0.1
		const config = paramControlConfig(scalar('x', 1));
		expect((config as { type: 'scalar'; step: number }).step).toBe(0.1);
	});

	it('uses d / 100 as step for large defaults', () => {
		// d=1000 → d/100=10, step should be 10
		const config = paramControlConfig(scalar('x', 1000));
		expect((config as { type: 'scalar'; step: number }).step).toBe(10);
	});

	it('handles zero default', () => {
		const config = paramControlConfig(scalar('x', 0));
		expect(config).toEqual({
			type: 'scalar',
			min: 0,
			max: 10,
			step: 0.1,
			initial: 0,
		});
	});

	it('handles negative default', () => {
		// d=-5 → min=-15, max=max(0, 5)=5, step=max(0.05, 0.1)=0.1
		const config = paramControlConfig(scalar('x', -5));
		expect(config).toEqual({
			type: 'scalar',
			min: -15,
			max: 5,
			step: 0.1,
			initial: -5,
		});
	});

	it('returns vector config for array defaults', () => {
		const config = paramControlConfig(vector('x', [1, 2, 3]));
		expect(config).toEqual({ type: 'vector', values: [1, 2, 3] });
	});

	it('preserves empty vector', () => {
		const config = paramControlConfig(vector('x', []));
		expect(config).toEqual({ type: 'vector', values: [] });
	});

	it('works for ArrivalRate kind', () => {
		const config = paramControlConfig(scalar('arrivals.Order', 6, 'ArrivalRate'));
		expect(config.type).toBe('scalar');
		expect((config as { initial: number }).initial).toBe(6);
	});

	it('works for WipLimit kind', () => {
		const config = paramControlConfig(scalar('Queue.wipLimit', 50, 'WipLimit'));
		expect((config as { initial: number }).initial).toBe(50);
	});

	it('min is never negative for positive default', () => {
		const config = paramControlConfig(scalar('x', 0.05));
		expect((config as { min: number }).min).toBeGreaterThanOrEqual(0);
	});
});

describe('kindLabel', () => {
	it.each([
		['ConstNode', 'const'],
		['ArrivalRate', 'arrival'],
		['WipLimit', 'wip'],
		['InitialCondition', 'init'],
	])('maps %s → %s', (kind, label) => {
		expect(kindLabel(kind)).toBe(label);
	});

	it('falls back to lowercased kind for unknown kinds', () => {
		expect(kindLabel('Unknown')).toBe('unknown');
	});
});
