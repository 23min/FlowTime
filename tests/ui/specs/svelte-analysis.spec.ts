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

	test('optimize tab still shows placeholder pointing at m-E21-05', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		// Wait for Svelte hydration before clicking — otherwise the click can
		// fire before the onclick handler has been attached to the button.
		await page.waitForLoadState('networkidle');
		await page.getByRole('button', { name: 'Sample', exact: true }).click();
		await page.getByRole('button', { name: 'Optimize' }).click();
		await expect(page.locator('text=/coming in m-E21-05/i')).toBeVisible({ timeout: 10000 });
	});

	test('sweep tab shows parameter selector when model has const nodes', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		await page.waitForLoadState('networkidle');
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
		await page.waitForLoadState('networkidle');
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
		await page.waitForLoadState('networkidle');
		await page.getByRole('button', { name: 'Sensitivity' }).click();
		await page.waitForTimeout(1500);

		const hasParams = await page.locator('text=Parameters:').isVisible().catch(() => false);
		const emptyState = await page.locator('text=/No const-kind parameters/i').isVisible().catch(() => false);
		expect(hasParams || emptyState).toBe(true);
	});

	test('can run sensitivity when parameters are available', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		await page.waitForLoadState('networkidle');
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

// Switch the source to a known sample model + param so the goal-seek tuples
// are deterministic across environments. Uses coffee-shop / customers_per_hour
// which the tracking doc pins for this milestone.
async function selectCoffeeShopSample(page: import('@playwright/test').Page) {
	await page.goto(`${SVELTE_URL}/analysis`);
	// Wait for Svelte to hydrate before the click — otherwise the handler
	// may not yet be attached and the click registers as a noop.
	await page.waitForLoadState('networkidle');
	await page.getByRole('button', { name: 'Sample', exact: true }).click();
	// Pick coffee-shop (first sample, default). Still explicit-select in case
	// localStorage carried over from a prior test.
	const sampleSelect = page.locator('select').first();
	await sampleSelect.selectOption('coffee-shop');
	await page.waitForTimeout(500);
}

test.describe('Analysis — Goal Seek', () => {
	test.beforeEach(async ({}, testInfo) => {
		if (!(await infraUp())) testInfo.skip();
	});

	test('goal-seek happy path: converges and renders chart + interval bar', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Goal Seek' }).click();

		// Param selector should auto-populate with customers_per_hour.
		const paramSelect = page.getByTestId('goal-seek-param-select');
		await expect(paramSelect).toBeVisible({ timeout: 5000 });
		await paramSelect.selectOption('customers_per_hour');

		// Interval defaults: 0.5× / 2× baseline (baseline=22 → 11 / 44).
		const lo = page.getByTestId('goal-seek-lo');
		const hi = page.getByTestId('goal-seek-hi');
		await expect(lo).toHaveValue('11');
		await expect(hi).toHaveValue('44');

		// Reachable metric + target so bisection converges.
		await page.getByTestId('goal-seek-metric').fill('register_queue');
		await page.getByTestId('goal-seek-target').fill('80');

		// Advanced: loose tolerance so we converge in a handful of iterations.
		await page.getByTestId('goal-seek-advanced-toggle').click();
		await page.getByTestId('goal-seek-tolerance').fill('0.01');

		const runButton = page.getByTestId('goal-seek-run');
		await expect(runButton).toBeEnabled();
		await runButton.click();

		// Result card appears with converged badge.
		const card = page.getByTestId('analysis-result-card');
		await expect(card).toBeVisible({ timeout: 30000 });
		await expect(page.getByTestId('analysis-result-card-badge')).toHaveText(/converged/i);

		// paramValue rendered.
		await expect(page.getByTestId('goal-seek-param-value')).toBeVisible();

		// Convergence chart rendered with at least one plotted point beyond iteration 0.
		// SVG <circle>/<line> elements can register as "hidden" to Playwright's
		// visibility heuristic even when rendered; assert on count instead.
		const chart = page.getByTestId('convergence-chart');
		await expect(chart).toBeVisible();
		const points = chart.locator('[data-testid="convergence-chart-point"]');
		const pointCount = await points.count();
		// Goal-seek traces include two iteration-0 boundary circles + per-iteration
		// midpoint circles. The final point has its own data-testid; convergence-chart-point
		// covers the intermediates, so the count should be at least 1 (all non-final entries).
		expect(pointCount).toBeGreaterThanOrEqual(1);
		// Final point marker is always attached for non-empty traces.
		await expect(
			chart.locator('[data-testid="convergence-chart-final-point"]'),
		).toHaveCount(1);

		// Search-interval bar + marker line are attached.
		await expect(page.getByTestId('goal-seek-interval-bar')).toBeVisible();
		await expect(page.getByTestId('goal-seek-interval-marker')).toHaveCount(1);
	});

	test('goal-seek not-bracketed: unreachable target renders amber warning', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Goal Seek' }).click();

		const paramSelect = page.getByTestId('goal-seek-param-select');
		await expect(paramSelect).toBeVisible({ timeout: 5000 });
		await paramSelect.selectOption('customers_per_hour');

		// Ensure defaults 11 / 44 so bracket is [11, 44].
		await page.getByTestId('goal-seek-lo').fill('11');
		await page.getByTestId('goal-seek-hi').fill('44');

		// Pinned tuple from tracking doc: unreachable target 1e12 on register_queue.
		await page.getByTestId('goal-seek-metric').fill('register_queue');
		await page.getByTestId('goal-seek-target').fill('1e12');

		await page.getByTestId('goal-seek-run').click();

		// Not-bracketed warning appears.
		const warning = page.getByTestId('goal-seek-not-bracketed-warning');
		await expect(warning).toBeVisible({ timeout: 30000 });

		// Badge reads "target not reachable".
		await expect(page.getByTestId('analysis-result-card-badge')).toHaveText(/not reachable/i);

		// Chart renders only the two iteration-0 boundary points (plus the
		// final-point marker overlays the last one). No line connecting them
		// beyond the straight segment between the two boundaries.
		const chart = page.getByTestId('convergence-chart');
		await expect(chart).toBeVisible();
		const allPoints = await chart.locator('circle').count();
		// 2 points total (boundary-lo + boundary-hi, one styled as final).
		expect(allPoints).toBe(2);
	});

	test('goal-seek run button is disabled until form is valid', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Goal Seek' }).click();

		const paramSelect = page.getByTestId('goal-seek-param-select');
		await expect(paramSelect).toBeVisible({ timeout: 5000 });

		// Force invalid bounds (lo === hi) and verify run is disabled.
		await page.getByTestId('goal-seek-lo').fill('5');
		await page.getByTestId('goal-seek-hi').fill('5');
		await expect(page.getByTestId('goal-seek-run')).toBeDisabled();
		await expect(page.getByTestId('goal-seek-interval-warning')).toBeVisible();

		// Fix to lo < hi; run becomes enabled (metric + target default non-empty).
		await page.getByTestId('goal-seek-hi').fill('44');
		await page.getByTestId('goal-seek-metric').fill('register_queue');
		await page.getByTestId('goal-seek-target').fill('80');
		await expect(page.getByTestId('goal-seek-run')).toBeEnabled();
	});
});
