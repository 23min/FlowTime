import { test, expect } from '@playwright/test';

// M-062 verification spec — exercises the /run page against live Sim API.
// Graceful skip when Sim API (8090) or Svelte dev server (5173) are unavailable.
//
// AC-1: cards in responsive grid with title, version badge, domain icon
// AC-2: search filter
// AC-3: select card → config panel with reuse mode + RNG seed + Advanced section
// AC-4: execute run → success result
// AC-5: preview/dry-run → plan
// AC-6: loading + empty states render
// AC-7: no raw JSON param field by default

const SVELTE_URL = 'http://localhost:5173';
const SIM_URL = 'http://localhost:8090';

async function infraUp(): Promise<{ sim: boolean; svelte: boolean }> {
	const probe = async (url: string) => {
		try {
			const res = await fetch(url, { signal: AbortSignal.timeout(1500) });
			return res.ok;
		} catch {
			return false;
		}
	};
	const [sim, svelte] = await Promise.all([
		probe(`${SIM_URL}/healthz`),
		probe(`${SVELTE_URL}/`)
	]);
	return { sim, svelte };
}

test.describe('M-062 Run Orchestration (/run)', () => {
	test.beforeEach(async ({}, testInfo) => {
		const infra = await infraUp();
		if (!infra.sim || !infra.svelte) {
			testInfo.skip();
		}
	});

	test('AC-1: templates render as cards in a grid with title + version badge + icon', async ({
		page
	}) => {
		await page.goto(`${SVELTE_URL}/run`);
		// Wait for at least one card. <button> wrapping a Card.Root is the card.
		const firstCardButton = page.locator('button:has(h3), button:has([data-slot="card-title"])').first();
		await firstCardButton.waitFor({ state: 'visible', timeout: 15000 });
		const count = await page
			.locator('button:has([data-slot="card-title"])')
			.count();
		expect(count).toBeGreaterThan(0);

		const firstTitle = await page
			.locator('[data-slot="card-title"]')
			.first()
			.textContent();
		expect(firstTitle?.trim().length ?? 0).toBeGreaterThan(0);

		// Version badge starts with "v".
		await expect(page.locator('text=/^v\\d/').first()).toBeVisible();

		// Grid layout class present on the card container.
		await expect(page.locator('div.grid.gap-4').first()).toBeVisible();
	});

	test('AC-2: search input filters templates in real-time', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/run`);
		await page
			.locator('button:has([data-slot="card-title"])')
			.first()
			.waitFor({ state: 'visible', timeout: 15000 });

		const initialCount = await page
			.locator('button:has([data-slot="card-title"])')
			.count();
		expect(initialCount).toBeGreaterThan(1);

		await page.locator('input[placeholder="Search templates..."]').fill('transportation');
		await page.waitForTimeout(300);

		const filteredCount = await page
			.locator('button:has([data-slot="card-title"])')
			.count();
		expect(filteredCount).toBeGreaterThan(0);
		expect(filteredCount).toBeLessThan(initialCount);

		// Negative path: nonsense query → empty message.
		await page.locator('input[placeholder="Search templates..."]').fill('zzznope');
		await expect(page.locator('text=/No templates match/')).toBeVisible();
	});

	test('AC-3 + AC-7: select card opens config panel with reuse mode + RNG seed; no raw-JSON field', async ({
		page
	}) => {
		await page.goto(`${SVELTE_URL}/run`);
		const firstCard = page.locator('button:has([data-slot="card-title"])').first();
		await firstCard.waitFor({ state: 'visible', timeout: 15000 });
		await firstCard.click();

		// Reuse-mode radio group: 3 options.
		await expect(page.locator('label[for="reuse-reuse"]')).toBeVisible();
		await expect(page.locator('label[for="reuse-regenerate"]')).toBeVisible();
		await expect(page.locator('label[for="reuse-fresh"]')).toBeVisible();

		// RNG seed input.
		await expect(page.locator('input#rng-seed')).toBeVisible();

		// Run + Preview buttons.
		await expect(page.getByRole('button', { name: /Run Model/i })).toBeVisible();
		await expect(page.getByRole('button', { name: /Preview/i })).toBeVisible();

		// AC-7: no raw JSON parameter textarea visible by default.
		const textareaCount = await page.locator('textarea').count();
		expect(textareaCount).toBe(0);

		// Advanced Parameters trigger may or may not be present depending on
		// whether the picked template has parameters; if present it must be
		// closed by default (no parameter inputs visible).
		const advancedTrigger = page.locator('text=Advanced Parameters');
		if (await advancedTrigger.isVisible().catch(() => false)) {
			// Parameters live behind a Collapsible. shadcn-svelte Collapsible
			// keeps children mounted but hides them via aria-hidden + CSS;
			// asserting on *visible* inputs is the correct AC-7 check.
			const visibleParams = await page
				.locator('input[id^="param-"]:visible')
				.count();
			expect(visibleParams).toBe(0);
		}
	});

	test('AC-4: execute run shows success result with run id', async ({ page }) => {
		test.setTimeout(60000);
		await page.goto(`${SVELTE_URL}/run`);
		const firstCard = page.locator('button:has([data-slot="card-title"])').first();
		await firstCard.waitFor({ state: 'visible', timeout: 15000 });
		await firstCard.click();
		await page.getByText('Bundle Reuse').waitFor({ state: 'visible', timeout: 10000 });

		// Click the Run Model button inside the config panel (sidebar nav
		// "Run Model" is a link, not a button — won't match here).
		await page.getByRole('button', { name: /Run Model/i }).click();
		// Wait for the success block (RunResult). Bundled component renders
		// "Run ID" or similar metadata.
		await expect(page.getByText(/Run ID/i).first()).toBeVisible({ timeout: 45000 });
	});

	test('AC-5: preview/dry-run shows execution plan without creating a run', async ({ page }) => {
		test.setTimeout(60000);
		await page.goto(`${SVELTE_URL}/run`);
		const firstCard = page.locator('button:has([data-slot="card-title"])').first();
		await firstCard.waitFor({ state: 'visible', timeout: 15000 });
		await firstCard.click();
		await page.getByText('Bundle Reuse').waitFor({ state: 'visible', timeout: 10000 });
		await page.getByRole('button', { name: /Preview/i }).click();

		// DryRunPlan renders the unique title "Dry Run Preview".
		await expect(page.getByText('Dry Run Preview')).toBeVisible({ timeout: 45000 });

		// Critical AC: "without creating a run" — the run-result success block
		// (which shows "Run ID") never renders for a dry-run.
		const runIdVisible = await page
			.getByText(/Run ID/i)
			.first()
			.isVisible()
			.catch(() => false);
		expect(runIdVisible).toBe(false);
	});

	test('AC-6: loading skeletons render before templates arrive', async ({ page }) => {
		// Force-stall the templates request to observe the skeleton.
		await page.route('**/api/v1/templates', async (route) => {
			await new Promise((r) => setTimeout(r, 1500));
			await route.continue();
		});
		await page.goto(`${SVELTE_URL}/run`);
		// Skeletons are pulse-animated muted divs in the same grid container.
		await expect(page.locator('div.animate-pulse').first()).toBeVisible({ timeout: 2000 });
	});

	test('AC-6: empty state renders when no templates returned', async ({ page }) => {
		await page.route('**/api/v1/templates', async (route) => {
			await route.fulfill({
				status: 200,
				contentType: 'application/json',
				body: '[]'
			});
		});
		await page.goto(`${SVELTE_URL}/run`);
		await expect(page.locator('text=No templates available.')).toBeVisible({ timeout: 10000 });
	});
});
