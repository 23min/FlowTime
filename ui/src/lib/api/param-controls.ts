// Parameter control configuration: maps a ParamInfo from the engine session
// into a UI control descriptor (range, step, type).
//
// Pure function — no DOM, no side effects. Tested in param-controls.test.ts.

import type { ParamInfo } from './engine-session.js';

export interface ScalarControlConfig {
	type: 'scalar';
	min: number;
	max: number;
	step: number;
	initial: number;
}

export interface VectorControlConfig {
	type: 'vector';
	values: number[];
}

export type ControlConfig = ScalarControlConfig | VectorControlConfig;

/**
 * Derive UI control config from a parameter info entry.
 *
 * Scalar: min = max(0, d × 0.1), max = max(d × 3, d + 10), step = max(d / 100, 0.1)
 *   Special cases:
 *     - d == 0: min = 0, max = 10, step = 0.1
 *     - d < 0: min = d × 3, max = max(0, d + 10), step based on |d|
 *
 * Vector: returns the values as-is (read-only display).
 */
export function paramControlConfig(param: ParamInfo): ControlConfig {
	if (Array.isArray(param.default)) {
		return { type: 'vector', values: param.default };
	}

	const d = param.default as number;

	if (d === 0) {
		return { type: 'scalar', min: 0, max: 10, step: 0.1, initial: 0 };
	}

	if (d < 0) {
		const absD = Math.abs(d);
		return {
			type: 'scalar',
			min: d * 3,
			max: Math.max(0, d + 10),
			step: Math.max(absD / 100, 0.1),
			initial: d,
		};
	}

	return {
		type: 'scalar',
		min: Math.max(0, d * 0.1),
		max: Math.max(d * 3, d + 10),
		step: Math.max(d / 100, 0.1),
		initial: d,
	};
}

/**
 * Get a display label for a parameter kind (for the badge UI).
 */
export function kindLabel(kind: string): string {
	switch (kind) {
		case 'ConstNode':
			return 'const';
		case 'ArrivalRate':
			return 'arrival';
		case 'WipLimit':
			return 'wip';
		case 'InitialCondition':
			return 'init';
		default:
			return kind.toLowerCase();
	}
}
