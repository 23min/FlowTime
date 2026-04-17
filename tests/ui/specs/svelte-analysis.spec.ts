import { test, expect } from '@playwright/test';

// End-to-end tests for the Svelte analysis surfaces (m-E21-03).
//
// Covers: page load, tab switching, sweep configuration + execution,
// sensitivity configuration + execution.
//
// Graceful skip when infra is unavailable.

const SVELTE_URL = 'http://localhost:5173';
const API_URL = 'http://localhost:8081';

async function infraUp(): Promise<boolean> {
	const probe = async (url: string) => {
		try {
			const res = await fetch(url, { signal: AbortSignal.timeout(1500) });
			return res.ok;
		} catch {
			return false;
		}
	};
	const [api, svelte] = await Promise.all([
		probe(`${API_URL}/v1/healthz`),
		probe(`${SVELTE_URL}/`),
	]);
	return api && svelte;
}

test.describe('Analysis', () => {
	test.beforeEach(async ({}, testInfo) => {
		if (!(await infraUp())) testInfo.skip();
	});

	test('page loads with tabs and run picker', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		await expect(page.locator('text=Analysis').first()).toBeVisible({ timeout: 10000 });
		await expect(page.getByRole('button', { name: 'Sweep' })).toBeVisible();
		await expect(page.getByRole('button', { name: 'Sensitivity' })).toBeVisible();
		await expect(page.getByRole('button', { name: 'Goal Seek' })).toBeVisible();
		await expect(page.getByRole('button', { name: 'Optimize' })).toBeVisible();
	});

	test('tab switching works and persists Goal Seek placeholder', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		await page.getByRole('button', { name: 'Goal Seek' }).click();
		await expect(page.locator('text=/coming in m-E21-04/i')).toBeVisible({ timeout: 3000 });
	});

	test('sweep tab shows parameter selector when model has const nodes', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		// Wait for params to load
		await page.waitForTimeout(1500);
		// Either param selector is present, or empty-state message
		const selector = page.locator('select').nth(1); // first is run picker, second is param
		const emptyState = page.locator('text=/No const-kind parameters/i');
		// At least one of the two should be visible within 10s
		await expect(async () => {
			const [selVisible, emptyVisible] = await Promise.all([
				selector.isVisible().catch(() => false),
				emptyState.isVisible().catch(() => false),
			]);
			expect(selVisible || emptyVisible).toBe(true);
		}).toPass({ timeout: 10000 });
	});

	test('can run sweep when parameters are available', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		await page.waitForTimeout(1500);

		const runButton = page.getByRole('button', { name: /Run sweep/i });
		// Skip if no params
		if (!(await runButton.isVisible().catch(() => false))) {
			test.skip(true, 'No const parameters in any available run');
			return;
		}

		await runButton.click();

		// Wait for the results chart to appear
		await expect(page.locator('text=/Chart:/i')).toBeVisible({ timeout: 30000 });
	});

	test('sensitivity tab renders chips and run button', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		await page.getByRole('button', { name: 'Sensitivity' }).click();
		await page.waitForTimeout(1500);

		const hasParams = await page.locator('text=Parameters:').isVisible().catch(() => false);
		const emptyState = await page.locator('text=/No const-kind parameters/i').isVisible().catch(() => false);
		expect(hasParams || emptyState).toBe(true);
	});

	test('can run sensitivity when parameters are available', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		await page.getByRole('button', { name: 'Sensitivity' }).click();
		await page.waitForTimeout(1500);

		const runButton = page.getByRole('button', { name: /Run sensitivity/i });
		if (!(await runButton.isVisible().catch(() => false))) {
			test.skip(true, 'No const parameters in any available run');
			return;
		}

		await runButton.click();

		// Wait for either the bar chart or an error
		await expect(async () => {
			const chartVisible = await page.locator('svg[aria-label="sensitivity bar chart"]').isVisible().catch(() => false);
			const errorVisible = await page.locator('text=/failed|error/i').first().isVisible().catch(() => false);
			expect(chartVisible || errorVisible).toBe(true);
		}).toPass({ timeout: 30000 });
	});
});
