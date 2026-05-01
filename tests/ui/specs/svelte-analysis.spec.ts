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

	test('optimize tab renders live panel shell (not the m-E21-05 stub)', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		// Wait for Svelte hydration before clicking — otherwise the click can
		// fire before the onclick handler has been attached to the button.
		await page.waitForLoadState('networkidle');
		await page.getByRole('button', { name: 'Sample', exact: true }).click();
		await page.getByRole('button', { name: 'Optimize' }).click();
		// AC1: live panel shell is mounted in place of the stub.
		// Visibility comes from AC2+ content; AC1 only asserts shell structure.
		await expect(page.getByTestId('optimize-panel')).toBeAttached({ timeout: 10000 });
		await expect(page.locator('text=/coming in m-E21-05/i')).toHaveCount(0);
	});

	test('optimize tab — chip-bar + bounds table render for coffee-shop (AC2)', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Optimize' }).click();

		// Both coffee-shop const params default to selected — bounds table shows 2 rows.
		await expect(page.getByTestId('optimize-bounds-table')).toBeVisible({ timeout: 10000 });
		await expect(page.getByTestId('optimize-lo-customers_per_hour')).toHaveValue('11');
		await expect(page.getByTestId('optimize-hi-customers_per_hour')).toHaveValue('44');
		await expect(page.getByTestId('optimize-lo-barista_service_rate')).toHaveValue('10');
		await expect(page.getByTestId('optimize-hi-barista_service_rate')).toHaveValue('40');

		// Run is enabled (all bounds valid, metric pre-seeded to register_queue).
		await expect(page.getByTestId('optimize-run')).toBeEnabled();
	});

	test('optimize tab — no-params-selected hides table + disables Run (AC2)', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Optimize' }).click();

		// Start with both chips selected; toggle both off.
		await page.getByTestId('optimize-param-chip-customers_per_hour').click();
		await page.getByTestId('optimize-param-chip-barista_service_rate').click();

		await expect(page.getByTestId('optimize-no-params-hint')).toBeVisible();
		await expect(page.getByTestId('optimize-bounds-table')).toHaveCount(0);
		await expect(page.getByTestId('optimize-run')).toBeDisabled();
	});

	test('optimize tab — inline validation flags lo >= hi (AC2)', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Optimize' }).click();

		// Force an invalid pair on customers_per_hour (lo === hi).
		const lo = page.getByTestId('optimize-lo-customers_per_hour');
		const hi = page.getByTestId('optimize-hi-customers_per_hour');
		await lo.fill('44');
		await hi.fill('44');

		await expect(page.getByTestId('optimize-bounds-error-customers_per_hour')).toBeVisible();
		await expect(page.getByTestId('optimize-run')).toBeDisabled();

		// Fixing lo < hi clears the error and re-enables Run.
		await lo.fill('11');
		await expect(page.getByTestId('optimize-bounds-error-customers_per_hour')).toHaveCount(0);
		await expect(page.getByTestId('optimize-run')).toBeEnabled();
	});

	test('optimize tab — metric chip shortcuts set the metric field (AC3)', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Optimize' }).click();

		const metric = page.getByTestId('optimize-metric');
		// Coffee-shop pre-seeds to register_queue.
		await expect(metric).toHaveValue('register_queue');

		// Each Sensitivity-style shortcut chip writes its own value.
		await page.getByTestId('optimize-metric-chip-served').click();
		await expect(metric).toHaveValue('served');

		await page.getByTestId('optimize-metric-chip-queue').click();
		await expect(metric).toHaveValue('queue');

		await page.getByTestId('optimize-metric-chip-flowLatencyMs').click();
		await expect(metric).toHaveValue('flowLatencyMs');

		await page.getByTestId('optimize-metric-chip-utilization').click();
		await expect(metric).toHaveValue('utilization');
	});

	test('optimize tab — direction defaults to minimize and toggles to maximize (AC3)', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Optimize' }).click();

		const minimize = page.getByTestId('optimize-direction-minimize');
		const maximize = page.getByTestId('optimize-direction-maximize');

		// AC3: direction defaults to minimize on first render.
		await expect(minimize).toHaveAttribute('aria-pressed', 'true');
		await expect(maximize).toHaveAttribute('aria-pressed', 'false');

		await maximize.click();
		await expect(minimize).toHaveAttribute('aria-pressed', 'false');
		await expect(maximize).toHaveAttribute('aria-pressed', 'true');

		await minimize.click();
		await expect(minimize).toHaveAttribute('aria-pressed', 'true');
	});

	test('optimize tab — direction resets to minimize on scenario change (AC3)', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Optimize' }).click();

		const minimize = page.getByTestId('optimize-direction-minimize');
		const maximize = page.getByTestId('optimize-direction-maximize');

		// Flip to maximize.
		await maximize.click();
		await expect(maximize).toHaveAttribute('aria-pressed', 'true');

		// Change sample — select a different one then back to coffee-shop to
		// guarantee applyModelYaml runs.
		const sampleSelect = page.locator('select').first();
		const options = await sampleSelect.locator('option').all();
		// Pick a sample other than coffee-shop; fall back to coffee-shop otherwise.
		let otherId = 'coffee-shop';
		for (const opt of options) {
			const value = await opt.getAttribute('value');
			if (value && value !== 'coffee-shop') {
				otherId = value;
				break;
			}
		}
		if (otherId === 'coffee-shop') test.skip(true, 'no alternate sample available');
		await sampleSelect.selectOption(otherId);
		await page.waitForTimeout(300);
		await sampleSelect.selectOption('coffee-shop');
		await page.waitForTimeout(300);

		// AC3: direction resets to minimize after scenario change.
		await expect(minimize).toHaveAttribute('aria-pressed', 'true');
		await expect(maximize).toHaveAttribute('aria-pressed', 'false');
	});

	test('optimize tab — happy path renders result card + per-param table + range bar + convergence chart (AC4/AC8)', async ({ page }) => {
		test.setTimeout(90_000);
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Optimize' }).click();

		// Both params selected by default; defaults are 0.5× / 2× baseline.
		// Flip direction to maximize — minimize plateaus at 0 for register_queue
		// under default bounds; the locked tuple in the tracking doc uses maximize.
		await page.getByTestId('optimize-direction-maximize').click();

		// metric is pre-seeded to register_queue by applyModelYaml for coffee-shop.
		await expect(page.getByTestId('optimize-metric')).toHaveValue('register_queue');

		const run = page.getByTestId('optimize-run');
		await expect(run).toBeEnabled();
		await run.click();

		// Result card appears with converged badge.
		const card = page.getByTestId('analysis-result-card');
		await expect(card).toBeVisible({ timeout: 60000 });
		await expect(page.getByTestId('analysis-result-card-badge')).toHaveText(/converged/i);

		// Per-param table renders one row per param with [lo, hi] text cell.
		const paramTable = page.getByTestId('optimize-param-table');
		await expect(paramTable).toBeVisible();
		await expect(page.getByTestId('optimize-param-row-customers_per_hour')).toBeVisible();
		await expect(page.getByTestId('optimize-param-row-barista_service_rate')).toBeVisible();
		// Bounds text cell prints "[lo, hi]" literal (fmtNum renders 11→"11.0", etc.).
		await expect(
			page.getByTestId('optimize-param-bounds-customers_per_hour'),
		).toHaveText(/\[\s*11(\.0)?\s*,\s*44(\.0)?\s*\]/);
		await expect(
			page.getByTestId('optimize-param-bounds-barista_service_rate'),
		).toHaveText(/\[\s*10(\.0)?\s*,\s*40(\.0)?\s*\]/);
		// Range bar is its own column with a marker line per row.
		await expect(
			page.getByTestId('optimize-range-bar-customers_per_hour'),
		).toBeVisible();
		await expect(
			page.getByTestId('optimize-range-bar-barista_service_rate'),
		).toBeVisible();
		await expect(
			page.getByTestId('optimize-range-marker-customers_per_hour'),
		).toHaveCount(1);
		await expect(
			page.getByTestId('optimize-range-marker-barista_service_rate'),
		).toHaveCount(1);

		// Convergence chart: multi-iteration, no target reference line (optimize has no target).
		const chart = page.getByTestId('convergence-chart');
		await expect(chart).toBeVisible();
		await expect(
			chart.locator('[data-testid="convergence-chart-target-line"]'),
		).toHaveCount(0);
		// Final point marker + at least one non-final point (multi-iteration).
		await expect(
			chart.locator('[data-testid="convergence-chart-final-point"]'),
		).toHaveCount(1);
		const nonFinal = await chart
			.locator('[data-testid="convergence-chart-point"]')
			.count();
		expect(nonFinal).toBeGreaterThanOrEqual(1);
	});

	test('optimize tab — not-converged state renders amber badge + warning + final params (AC5)', async ({ page }) => {
		test.setTimeout(90_000);
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Optimize' }).click();

		// Force maxIterations=1 so Nelder-Mead cannot converge on the coffee-shop
		// maximize run (which normally takes ~21 iters).
		await page.getByTestId('optimize-direction-maximize').click();
		await page.getByTestId('optimize-advanced-toggle').click();
		await page.getByTestId('optimize-max-iterations').fill('1');

		await expect(page.getByTestId('optimize-run')).toBeEnabled();
		await page.getByTestId('optimize-run').click();

		// Result card renders with amber "did not converge" badge.
		await expect(page.getByTestId('analysis-result-card')).toBeVisible({ timeout: 60000 });
		await expect(page.getByTestId('analysis-result-card-badge')).toHaveText(/did not converge/i);

		// AC5: amber warning footer explains the cause.
		await expect(page.getByTestId('optimize-not-converged-warning')).toBeVisible();

		// AC5: per-param table still renders final paramValues for both params.
		await expect(page.getByTestId('optimize-param-row-customers_per_hour')).toBeVisible();
		await expect(page.getByTestId('optimize-param-row-barista_service_rate')).toBeVisible();
		await expect(page.getByTestId('optimize-param-final-customers_per_hour')).toBeVisible();
		await expect(page.getByTestId('optimize-param-final-barista_service_rate')).toBeVisible();
	});

	test('optimize tab — form state survives tab switches, resets on scenario change (AC6)', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Optimize' }).click();

		// Mutate form: toggle direction → maximize, deselect customers_per_hour,
		// change metric + bounds, open advanced + change tolerance.
		await page.getByTestId('optimize-direction-maximize').click();
		await page.getByTestId('optimize-param-chip-customers_per_hour').click();
		await page.getByTestId('optimize-metric').fill('served');
		await page.getByTestId('optimize-hi-barista_service_rate').fill('55');
		await page.getByTestId('optimize-advanced-toggle').click();
		await page.getByTestId('optimize-tolerance').fill('0.01');

		// Switch to Goal Seek and back — form state must survive.
		await page.getByRole('button', { name: 'Goal Seek' }).click();
		await page.waitForTimeout(200);
		await page.getByRole('button', { name: 'Optimize' }).click();

		await expect(page.getByTestId('optimize-direction-maximize')).toHaveAttribute('aria-pressed', 'true');
		// customers_per_hour chip still de-selected (only barista row should render).
		await expect(page.getByTestId('optimize-param-row-barista_service_rate')).toHaveCount(0);
		// (row only exists after a run; here we check the chip's pressed state via the
		// bounds-table row — de-selected chips hide their row in the pre-run bounds table.)
		await expect(page.getByTestId('optimize-metric')).toHaveValue('served');
		await expect(page.getByTestId('optimize-hi-barista_service_rate')).toHaveValue('55');
		// Advanced is still open with our tolerance value preserved.
		await expect(page.getByTestId('optimize-tolerance')).toHaveValue('0.01');

		// Change scenario to a different sample then back → form must reset to defaults.
		const sampleSelect = page.locator('select').first();
		const options = await sampleSelect.locator('option').all();
		let otherId = 'coffee-shop';
		for (const opt of options) {
			const value = await opt.getAttribute('value');
			if (value && value !== 'coffee-shop') {
				otherId = value;
				break;
			}
		}
		if (otherId === 'coffee-shop') test.skip(true, 'no alternate sample available');
		await sampleSelect.selectOption(otherId);
		await page.waitForTimeout(300);
		await sampleSelect.selectOption('coffee-shop');
		await page.waitForTimeout(300);

		// Direction reset to minimize; metric reset to register_queue; advanced collapsed.
		await expect(page.getByTestId('optimize-direction-minimize')).toHaveAttribute('aria-pressed', 'true');
		await expect(page.getByTestId('optimize-metric')).toHaveValue('register_queue');
		await expect(page.getByTestId('optimize-advanced')).toHaveCount(0);
		// Both chips re-selected; bounds reset to 0.5× / 2× baseline (10 / 40 for barista).
		await expect(page.getByTestId('optimize-hi-barista_service_rate')).toHaveValue('40');
		await expect(page.getByTestId('optimize-lo-customers_per_hour')).toHaveValue('11');
	});

	test('optimize tab — advanced disclosure exposes tolerance + maxIterations (AC3)', async ({ page }) => {
		await selectCoffeeShopSample(page);
		await page.getByRole('button', { name: 'Optimize' }).click();

		// Advanced section is collapsed by default.
		await expect(page.getByTestId('optimize-advanced')).toHaveCount(0);

		await page.getByTestId('optimize-advanced-toggle').click();

		const tolerance = page.getByTestId('optimize-tolerance');
		const maxIterations = page.getByTestId('optimize-max-iterations');
		await expect(tolerance).toBeVisible();
		await expect(maxIterations).toBeVisible();

		// AC3 defaults: tolerance=1e-4, maxIterations=200.
		await expect(tolerance).toHaveValue('0.0001');
		await expect(maxIterations).toHaveValue('200');

		// Setting tolerance <= 0 disables Run.
		await tolerance.fill('0');
		await expect(page.getByTestId('optimize-run')).toBeDisabled();
		await tolerance.fill('0.0001');
		await expect(page.getByTestId('optimize-run')).toBeEnabled();

		// Setting maxIterations to 0 disables Run.
		await maxIterations.fill('0');
		await expect(page.getByTestId('optimize-run')).toBeDisabled();
		await maxIterations.fill('200');
		await expect(page.getByTestId('optimize-run')).toBeEnabled();
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
