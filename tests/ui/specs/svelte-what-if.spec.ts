import { test, expect, type Page } from '@playwright/test';

// End-to-end test for the Svelte What-If page (m-E17-02).
//
// This test drives a real browser against the Svelte UI on port 5173
// and the FlowTime API on port 8081. It verifies:
// - Page loads and renders the model picker
// - Selecting a model populates the parameter panel and series display
// - Tweaking a parameter triggers a new eval and updates the series
// - The latency badge shows a value
// - Reset button restores defaults
// - Switching models produces the expected parameter set
//
// Graceful skip: if either the API or the Svelte dev server is not running,
// the tests are skipped (not failed) so the spec doesn't fail in an
// environment where the infrastructure isn't up. To run locally:
//   1. Terminal A: dotnet run --project src/FlowTime.API
//   2. Terminal B: cd ui && pnpm dev
//   3. Terminal C: npm run test-ui -- --grep "What-If"

const SVELTE_URL = 'http://localhost:5173';
const API_URL = 'http://localhost:8081';

async function infraUp(): Promise<{ api: boolean; svelte: boolean }> {
	const probe = async (url: string) => {
		try {
			const res = await fetch(url, { signal: AbortSignal.timeout(1500) });
			return res.ok;
		} catch {
			return false;
		}
	};
	const [api, svelte] = await Promise.all([
		probe(`${API_URL}/v1/engine/session/health`),
		probe(`${SVELTE_URL}/what-if`),
	]);
	return { api, svelte };
}

async function waitForReady(page: Page): Promise<void> {
	// Wait for the compile to finish and the ready state to appear
	await page.waitForSelector('[data-testid="what-if-ready"]', { timeout: 15000 });
	await page.waitForSelector('[data-testid="param-panel"]', { timeout: 5000 });
}

async function getSeriesValues(page: Page, name: string): Promise<number[]> {
	const text = await page.locator(`[data-testid="series-values-${name}"]`).innerText();
	// Parse "[1, 2, 3]" or "[1.50, 2.50]" into number[]
	const stripped = text.replace(/[[\]\s]/g, '');
	if (!stripped) return [];
	return stripped.split(',').map(Number);
}

test.describe('What-If page', () => {
	test.beforeAll(async () => {
		const { api, svelte } = await infraUp();
		if (!api || !svelte) {
			test.skip(
				true,
				`Infra down: api=${api}, svelte=${svelte}. Start API (8081) + Svelte dev server (5173).`,
			);
		}
	});

	test('loads and renders model picker', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await expect(page.locator('[data-testid="what-if-page"]')).toBeVisible();
		await expect(page.locator('[data-testid="model-picker"]')).toBeVisible();
		await expect(page.locator('[data-testid="model-button-simple-pipeline"]')).toBeVisible();
		await expect(page.locator('[data-testid="model-button-queue-with-wip"]')).toBeVisible();
		await expect(page.locator('[data-testid="model-button-class-decomposition"]')).toBeVisible();
	});

	test('default model compiles and shows parameters + series + latency', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Simple pipeline has one param: arrivals
		await expect(page.locator('[data-testid="param-row-arrivals"]')).toBeVisible();

		// Series should include arrivals and served
		await expect(page.locator('[data-testid="series-row-arrivals"]')).toBeVisible();
		await expect(page.locator('[data-testid="series-row-served"]')).toBeVisible();

		// Latency badge should appear with a numeric value
		await expect(page.locator('[data-testid="latency-badge"]')).toBeVisible();
		const latencyText = await page.locator('[data-testid="latency-us"]').innerText();
		expect(parseInt(latencyText, 10)).toBeGreaterThanOrEqual(0);

		// Initial: arrivals = 10 (const [10,10,10,10]), served = arrivals * 0.8 = [8, 8, 8, 8]
		const arrivals = await getSeriesValues(page, 'arrivals');
		expect(arrivals).toEqual([10, 10, 10, 10]);
		const served = await getSeriesValues(page, 'served');
		expect(served).toEqual([8, 8, 8, 8]);
	});

	test('tweaking a parameter updates the series', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Change arrivals from 10 to 20 via the numeric input
		const input = page.locator('[data-testid="input-arrivals"]');
		await input.fill('20');

		// Wait for the debounced eval (input debounce = 150ms + eval latency)
		await page.waitForFunction(
			() => {
				const el = document.querySelector('[data-testid="series-values-served"]');
				if (!el) return false;
				return el.textContent?.includes('16');
			},
			{ timeout: 3000 },
		);

		// served = 20 * 0.8 = 16
		const served = await getSeriesValues(page, 'served');
		expect(served).toEqual([16, 16, 16, 16]);

		// Arrivals series should also have updated
		const arrivals = await getSeriesValues(page, 'arrivals');
		expect(arrivals).toEqual([20, 20, 20, 20]);
	});

	test('reset button restores defaults', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Tweak first
		await page.locator('[data-testid="input-arrivals"]').fill('50');
		await page.waitForFunction(
			() => {
				const el = document.querySelector('[data-testid="series-values-served"]');
				return el?.textContent?.includes('40');
			},
			{ timeout: 3000 },
		);

		// Reset
		await page.locator('[data-testid="reset-button"]').click();
		await page.waitForFunction(
			() => {
				const el = document.querySelector('[data-testid="series-values-served"]');
				return el?.textContent?.includes('8,');
			},
			{ timeout: 3000 },
		);

		const served = await getSeriesValues(page, 'served');
		expect(served).toEqual([8, 8, 8, 8]);
	});

	test('switching model updates parameter set', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Initially simple-pipeline: one param (arrivals)
		await expect(page.locator('[data-testid="param-row-arrivals"]')).toBeVisible();

		// Switch to queue-with-wip: has arrivals, served, Queue.wipLimit
		await page.locator('[data-testid="model-button-queue-with-wip"]').click();

		// Wait for new parameter panel to render
		await page.waitForSelector('[data-testid="param-row-Queue.wipLimit"]', { timeout: 10000 });

		await expect(page.locator('[data-testid="param-row-arrivals"]')).toBeVisible();
		await expect(page.locator('[data-testid="param-row-served"]')).toBeVisible();
		await expect(page.locator('[data-testid="param-row-Queue.wipLimit"]')).toBeVisible();
	});

	test('class model shows class rate parameters', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		await page.locator('[data-testid="model-button-class-decomposition"]').click();
		await page.waitForSelector('[data-testid="param-row-arrivals.Order"]', { timeout: 10000 });

		await expect(page.locator('[data-testid="param-row-arrivals.Order"]')).toBeVisible();
		await expect(page.locator('[data-testid="param-row-arrivals.Refund"]')).toBeVisible();

		// served = MIN(arrivals, 8), arrivals=10 → served=[8,8,8,8]
		const served = await getSeriesValues(page, 'served');
		expect(served).toEqual([8, 8, 8, 8]);
	});

	test('wip limit override affects queue depth', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Switch to queue-with-wip
		await page.locator('[data-testid="model-button-queue-with-wip"]').click();
		await page.waitForSelector('[data-testid="param-row-Queue.wipLimit"]', { timeout: 10000 });

		// Default: WIP=50, inflow=20, outflow=5 → Q grows 15/bin then caps at 50
		// Q = [15, 30, 45, 50, 50, 50]
		await page.waitForFunction(
			() => {
				const el = document.querySelector('[data-testid="series-values-queue_queue"]');
				return el?.textContent?.includes('15');
			},
			{ timeout: 5000 },
		);
		let q = await getSeriesValues(page, 'queue_queue');
		expect(q).toEqual([15, 30, 45, 50, 50, 50]);

		// Lower WIP to 25 → Q should cap earlier: [15, 25, 25, 25, 25, 25]
		await page.locator('[data-testid="input-Queue.wipLimit"]').fill('25');
		await page.waitForFunction(
			() => {
				const el = document.querySelector('[data-testid="series-values-queue_queue"]');
				return el?.textContent?.includes('15, 25,');
			},
			{ timeout: 3000 },
		);

		q = await getSeriesValues(page, 'queue_queue');
		expect(q).toEqual([15, 25, 25, 25, 25, 25]);
	});

	// ── m-E17-03 additions: topology graph + interactive charts ──

	test('topology graph renders after compile', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Topology panel should be visible with the graph inside
		await expect(page.locator('[data-testid="topology-panel"]')).toBeVisible();
		await expect(page.locator('[data-testid="topology-graph"]')).toBeVisible();
		// The graph should have rendered an SVG
		await expect(page.locator('[data-testid="topology-graph"] svg').first()).toBeVisible();
	});

	test('topology heatmap nodes have distinct colors for distinct series', async ({ page }) => {
		// Simple pipeline: arrivals mean = 10, served mean = 8 → different colors
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Extract fill colors from the rendered SVG. dag-map renders each node
		// with a fill attribute that comes from colorScale(t) — different t
		// values should produce different rgb(...) strings.
		const fills: string[] = await page.evaluate(() => {
			const svg = document.querySelector('[data-testid="topology-graph"] svg');
			if (!svg) return [];
			const results: string[] = [];
			// dag-map renders nodes as circles or filled shapes — check all fill attrs
			svg.querySelectorAll('[fill]').forEach((el) => {
				const f = el.getAttribute('fill');
				if (f && f.startsWith('rgb')) results.push(f);
			});
			return results;
		});

		// We expect at least 2 distinct rgb() values when there are nodes with
		// different metrics. (Not 1 — that would mean all-same color, the bug.)
		const unique = new Set(fills);
		expect(unique.size).toBeGreaterThanOrEqual(2);
	});

	test('chart component renders for each non-internal series', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Chart components should be rendered (one per series)
		const charts = page.locator('[data-testid="chart"]');
		const count = await charts.count();
		// simple-pipeline has 2 series → 2 charts
		expect(count).toBeGreaterThanOrEqual(2);
	});

	test('chart hover shows tooltip with values', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Hover the SVG inside the first chart using locator.hover which
		// reliably dispatches mousemove to the target element
		const chartSvg = page.locator('[data-testid="chart"] svg').first();
		await chartSvg.hover({ position: { x: 160, y: 70 } });

		// Tooltip should appear
		const tooltip = page.locator('[data-testid="chart-tooltip"]').first();
		await expect(tooltip).toBeVisible({ timeout: 2000 });
		const text = await tooltip.innerText();
		expect(text).toMatch(/bin \d/);
	});

	test('layout stability — topology DOM structure does not change on parameter tweak', async ({
		page,
	}) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);
		// Switch to queue-with-wip which has a real topology graph
		await page.locator('[data-testid="model-button-queue-with-wip"]').click();
		await page.waitForSelector('[data-testid="param-row-Queue.wipLimit"]', { timeout: 10000 });

		// Capture node positions before tweak
		const svgBefore = await page
			.locator('[data-testid="topology-graph"] svg')
			.first()
			.innerHTML();

		// Tweak WIP limit
		await page.locator('[data-testid="input-Queue.wipLimit"]').fill('30');
		await page.waitForFunction(
			() => {
				const el = document.querySelector('[data-testid="series-values-queue_queue"]');
				return el?.textContent?.includes('30,');
			},
			{ timeout: 3000 },
		);

		// Capture after
		const svgAfter = await page
			.locator('[data-testid="topology-graph"] svg')
			.first()
			.innerHTML();

		// The SVG structure (nodes, edges, layout) should be stable across evals.
		// Colors (fill) may change — strip them before comparing.
		const stripDynamic = (html: string) =>
			html
				.replace(/fill="[^"]*"/g, 'fill=""')
				.replace(/stroke="[^"]*"/g, 'stroke=""')
				.replace(/style="[^"]*"/g, 'style=""');

		expect(stripDynamic(svgAfter)).toBe(stripDynamic(svgBefore));
	});

	test('model switch recreates topology graph', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Topology for simple-pipeline
		const simplePipelineSvg = await page
			.locator('[data-testid="topology-graph"] svg')
			.first()
			.innerHTML();

		// Switch to queue-with-wip
		await page.locator('[data-testid="model-button-queue-with-wip"]').click();
		await page.waitForSelector('[data-testid="param-row-Queue.wipLimit"]', { timeout: 10000 });

		// Topology should be different (new structure)
		const queueSvg = await page
			.locator('[data-testid="topology-graph"] svg')
			.first()
			.innerHTML();

		expect(queueSvg).not.toBe(simplePipelineSvg);
	});

	test('class decomposition shows per-class series that update on class rate tweak', async ({
		page,
	}) => {
		await page.goto(`${SVELTE_URL}/what-if`);
		await waitForReady(page);

		// Switch to class decomposition
		await page.locator('[data-testid="model-button-class-decomposition"]').click();
		await page.waitForSelector('[data-testid="param-row-arrivals.Order"]', { timeout: 10000 });

		// Per-class series should be visible (previously filtered out as "internal")
		await expect(
			page.locator('[data-testid="series-values-arrivals__class_Order"]'),
		).toBeVisible();
		await expect(
			page.locator('[data-testid="series-values-arrivals__class_Refund"]'),
		).toBeVisible();
		await expect(
			page.locator('[data-testid="series-values-served__class_Order"]'),
		).toBeVisible();

		// Default: arrivals.Order=6, arrivals.Refund=4, total arrivals=10
		// After normalization: Order=6, Refund=4 (sum matches total — no rescaling)
		// served = MIN(10, 8) = 8. Per-class served: 8 × 0.6 = 4.8, 8 × 0.4 = 3.2
		const servedOrder = await getSeriesValues(page, 'served__class_Order');
		expect(servedOrder[0]).toBeCloseTo(4.8, 1);

		// Tweak arrivals.Order to 12 → new class_sum = 16, Order fraction = 12/16 = 0.75
		// But normalization rescales to total=10: Order = 10 × (12/16) = 7.5
		// served_Order = 8 × (7.5/10) = 6.0
		await page.locator('[data-testid="input-arrivals.Order"]').fill('12');
		await page.waitForFunction(
			() => {
				const el = document.querySelector('[data-testid="series-values-served__class_Order"]');
				return el?.textContent?.includes('6');
			},
			{ timeout: 3000 },
		);

		const servedOrderAfter = await getSeriesValues(page, 'served__class_Order');
		expect(servedOrderAfter[0]).toBeCloseTo(6.0, 1);

		// Refund per-class should have decreased correspondingly
		const servedRefundAfter = await getSeriesValues(page, 'served__class_Refund');
		expect(servedRefundAfter[0]).toBeCloseTo(2.0, 1);
	});
});
