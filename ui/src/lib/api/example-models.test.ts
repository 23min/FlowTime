import { describe, it, expect } from 'vitest';
import { EXAMPLE_MODELS, findExampleModel } from './example-models';

describe('EXAMPLE_MODELS', () => {
	it('exports exactly 4 models', () => {
		expect(EXAMPLE_MODELS).toHaveLength(4);
	});

	it('each model has unique id', () => {
		const ids = EXAMPLE_MODELS.map((m) => m.id);
		expect(new Set(ids).size).toBe(ids.length);
	});

	it('each model has name, description, and non-empty yaml', () => {
		for (const model of EXAMPLE_MODELS) {
			expect(model.id).toBeTruthy();
			expect(model.name).toBeTruthy();
			expect(model.description).toBeTruthy();
			expect(model.yaml.length).toBeGreaterThan(50);
		}
	});

	it('yaml contains required grid section', () => {
		for (const model of EXAMPLE_MODELS) {
			expect(model.yaml).toContain('grid:');
			expect(model.yaml).toContain('bins:');
		}
	});

	it('includes expected model ids', () => {
		const ids = EXAMPLE_MODELS.map((m) => m.id).sort();
		expect(ids).toEqual([
			'capacity-constrained',
			'class-decomposition',
			'queue-with-wip',
			'simple-pipeline',
		]);
	});
});

describe('findExampleModel', () => {
	it('returns the matching model by id', () => {
		const model = findExampleModel('simple-pipeline');
		expect(model).toBeDefined();
		expect(model?.id).toBe('simple-pipeline');
	});

	it('returns undefined for unknown id', () => {
		expect(findExampleModel('nonexistent')).toBeUndefined();
	});

	it('is case-sensitive', () => {
		expect(findExampleModel('Simple-Pipeline')).toBeUndefined();
	});

	it('returns undefined for empty string', () => {
		expect(findExampleModel('')).toBeUndefined();
	});
});
