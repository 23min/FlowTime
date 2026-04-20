import { test, expect } from '@playwright/test';

// End-to-end tests for the ultrareview follow-up fixes on /analysis:
//   - selectedSampleId persists across reload (Finding 2)
//   - sweep truncation at 200 points surfaces a distinct warning (Finding 3)
//
// Finding 4 (topology CSS.escape on edge selectors) is covered by the
// vitest unit test at `ui/src/lib/utils/topology-selectors.test.ts` —
// the pure selector-building helper is asserted against every special
// character class the guard is designed to handle. The try/catch
// fallback in the topology $effect is a defensive second line that
// would only trigger if a future CSS specification reveals an
// unescapable input; no dedicated end-to-end test is needed.
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

test.describe('Analysis — ultrareview follow-ups', () => {
	test.beforeEach(async ({}, testInfo) => {
		if (!(await infraUp())) testInfo.skip();
	});

	test('selectedSampleId persists across reload in sample mode', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		await page.waitForTimeout(500);

		// Switch to sample mode if not already there.
		const sampleButton = page.getByRole('button', { name: /Sample/i }).first();
		if (await sampleButton.isVisible().catch(() => false)) {
			await sampleButton.click();
		}

		// The sample selector is a <select> bound to selectedSampleId.
		const sampleSelect = page.locator('select[data-testid="sample-select"], select').filter({
			has: page.locator('option'),
		}).last();

		// Read the available options and pick one that is not the current value.
		const options = await sampleSelect.locator('option').evaluateAll((els) =>
			els.map((e) => ({ value: (e as HTMLOptionElement).value, selected: (e as HTMLOptionElement).selected }))
		);
		if (options.length < 2) {
			test.skip(true, 'Need at least 2 sample models to test persistence');
			return;
		}
		const target = options.find((o) => !o.selected) ?? options[1];

		await sampleSelect.selectOption(target.value);
		await page.waitForTimeout(300);

		// Verify localStorage captured the choice.
		const stored = await page.evaluate(() => localStorage.getItem('ft.analysis.sample'));
		expect(stored).toBe(target.value);

		// Reload — the persisted choice must survive.
		await page.reload();
		await page.waitForTimeout(500);

		const persistedValue = await sampleSelect.inputValue();
		expect(persistedValue).toBe(target.value);
	});

	test('sweep truncation at 200 points shows distinct warning', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/analysis`);
		await page.waitForTimeout(1000);

		// Ensure we are on the Sweep tab.
		await page.getByRole('button', { name: 'Sweep' }).click();
		await page.waitForTimeout(300);

		const fromInput = page.locator('input[type="number"]').nth(0);
		const toInput = page.locator('input[type="number"]').nth(1);
		const stepInput = page.locator('input[type="number"]').nth(2);

		if (!(await fromInput.isVisible().catch(() => false))) {
			test.skip(true, 'Sweep range inputs unavailable (no params in current model)');
			return;
		}

		// Request 1001 points → the generator caps at 200. Expect the
		// distinct truncation warning, not the generic > 50 one.
		await fromInput.fill('0');
		await toInput.fill('1000');
		await stepInput.fill('1');
		await page.waitForTimeout(200);

		await expect(page.locator('[data-testid="sweep-truncation-warning"]')).toBeVisible();
		await expect(page.locator('[data-testid="sweep-truncation-warning"]')).toContainText(/truncated/i);
		await expect(page.locator('[data-testid="sweep-truncation-warning"]')).toContainText(/1001/);

		// Now request 60 points — over the soft > 50 limit but not clipped.
		// The distinct truncation warning must disappear; the generic one shows.
		await fromInput.fill('0');
		await toInput.fill('59');
		await stepInput.fill('1');
		await page.waitForTimeout(200);

		await expect(page.locator('[data-testid="sweep-truncation-warning"]')).not.toBeVisible();
		await expect(page.locator('text=/large sweep/i')).toBeVisible();
	});
});
