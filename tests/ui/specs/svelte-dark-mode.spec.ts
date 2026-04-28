import { test, expect } from '@playwright/test';

// m-E21-08 AC4 — dark-mode smoke spec.
//
// Audits that the chrome tokens resolve and surfaces render correctly in
// dark mode. Smoke-level: confirms theme attribute lands, at least one
// indicator dot is visible against the near-black background, and the
// /analysis route renders. Deeper visual regression (pixel snapshots) is
// out of scope per AC4.
//
// Graceful skip: API or Svelte dev server not running.

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
		probe(`${API_URL}/v1/healthz`),
		probe(`${SVELTE_URL}/`),
	]);
	return { api, svelte };
}

async function setDarkMode(page: import('@playwright/test').Page) {
	// Pre-set the theme storage so the page renders dark from first paint.
	// Mirrors the theme store's STORAGE_KEY at ui/src/lib/stores/theme.svelte.ts:5.
	await page.addInitScript(() => {
		try {
			window.localStorage.setItem('ft.theme', 'dark');
		} catch {
			// Storage may be unavailable in some test contexts — ignore.
		}
	});
}

test.describe('Dark mode smoke (m-E21-08 AC4)', () => {
	test.beforeEach(async ({}, testInfo) => {
		const infra = await infraUp();
		if (!infra.api || !infra.svelte) {
			testInfo.skip();
		}
	});

	test('topology page renders in dark mode with chrome tokens resolving', async ({
		page,
	}) => {
		await setDarkMode(page);
		await page.goto(`${SVELTE_URL}/time-travel/topology`);

		// `<html>` carries `.dark` once the theme effect runs.
		await expect(page.locator('html.dark')).toBeVisible({ timeout: 10000 });

		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// `--ft-focus` chrome token resolves (m-E21-08 AC1).
		const focusValue = await page.evaluate(() =>
			getComputedStyle(document.documentElement).getPropertyValue('--ft-focus').trim(),
		);
		expect(focusValue).toBeTruthy();

		// `--ft-highlight` chrome token resolves (m-E21-06 / AC2).
		const highlightValue = await page.evaluate(() =>
			getComputedStyle(document.documentElement).getPropertyValue('--ft-highlight').trim(),
		);
		expect(highlightValue).toBeTruthy();

		// Body background is near-black in dark mode (chrome reads against it).
		const bg = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
		// HSL converted to rgb — the dark-mode --background is hsl(220 12% 5%).
		// Just assert it's not the default white.
		expect(bg).not.toBe('rgb(255, 255, 255)');
	});

	test('topology warning indicators render with severity tokens against dark chrome', async ({
		page,
	}) => {
		await setDarkMode(page);
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// Warning indicators use --ft-warn / --ft-err / --ft-info chrome tokens.
		// They render only when the loaded run carries warnings; tolerate runs
		// without warnings (smoke gate).
		const indicators = page.locator('[data-warning-indicator]');
		const count = await indicators.count();
		if (count === 0) {
			test.skip(true, 'Run has no warnings; severity-token rendering not exercised');
			return;
		}

		// At least one indicator must be visible (non-zero size, painted).
		const first = indicators.first();
		await expect(first).toBeVisible();

		// Severity-driven token must resolve to a non-empty colour string.
		const severity = await first.getAttribute('data-warning-severity');
		expect(severity).toBeTruthy();
		const tokenName =
			severity === 'error' ? '--ft-err' : severity === 'info' ? '--ft-info' : '--ft-warn';
		const tokenValue = await page.evaluate(
			(name) => getComputedStyle(document.documentElement).getPropertyValue(name).trim(),
			tokenName,
		);
		expect(tokenValue).toBeTruthy();
	});

	test('analysis page renders in dark mode', async ({ page }) => {
		await setDarkMode(page);
		await page.goto(`${SVELTE_URL}/analysis`);
		await expect(page.locator('html.dark')).toBeVisible({ timeout: 10000 });

		// At least one of the four tab buttons renders (sweep/sensitivity/goal-seek/optimize).
		await expect(page.getByRole('button', { name: 'Sweep', exact: true })).toBeVisible({
			timeout: 10000,
		});
	});

	test('selection chrome (--ft-highlight) reads correctly against dark background', async ({
		page,
	}) => {
		await setDarkMode(page);
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// Close any auto-pinned cards so the click below pins (rather than toggle-unpins).
		const initial = await page.locator('button[aria-label^="Unpin"]').count();
		for (let i = 0; i < initial; i++) {
			await page.locator('button[aria-label^="Unpin"]').first().click();
		}

		const firstNode = page.locator('[data-node-id]').first();
		await firstNode.click();

		// Selection class lands; the rule strokes the inner circle with --ft-highlight.
		const selectedNode = page.locator('[data-node-id].node-selected').first();
		await expect(selectedNode).toBeVisible({ timeout: 5000 });

		// Inner circle's computed stroke must be non-default (not 'none' / not transparent).
		const stroke = await selectedNode
			.locator('circle')
			.first()
			.evaluate((el) => getComputedStyle(el).stroke);
		expect(stroke).not.toBe('none');
		expect(stroke).not.toMatch(/^rgba\(0,\s*0,\s*0,\s*0\)$/);
	});
});
