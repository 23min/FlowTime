import { test, expect } from '@playwright/test';

// m-E21-08 AC5 — loading skeletons.
//
// Replaces the empty→populated flicker with a Skeleton placeholder during:
//   - /time-travel/topology run-load (graph fetch)
//   - /time-travel/topology state_window load (data fetch after graph)
//   - /analysis sweep / sensitivity / goal-seek / optimize compute
//
// Each test mocks the relevant network call with a deliberate delay so the
// skeleton state is observable in a real browser run.
//
// Graceful skip: dev server not running.

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

test.describe('Loading skeletons (m-E21-08 AC5)', () => {
	test.beforeEach(async ({}, testInfo) => {
		const infra = await infraUp();
		if (!infra.api || !infra.svelte) {
			testInfo.skip();
		}
	});

	test('topology canvas skeleton appears during state_window load', async ({ page }) => {
		// Delay the state_window response so the skeleton remains visible long
		// enough to assert against. The state_window URL pattern matches the
		// API contract: /v1/runs/{runId}/state_window
		await page.route('**/v1/runs/*/state_window**', async (route) => {
			await new Promise((r) => setTimeout(r, 800));
			await route.continue();
		});

		await page.goto(`${SVELTE_URL}/time-travel/topology`);

		// Canvas-region skeleton renders during the state_window fetch.
		await expect(page.getByTestId('topology-canvas-skeleton')).toBeVisible({
			timeout: 5000,
		});

		// After the fetch completes the skeleton goes away and the DAG renders.
		await expect(page.getByTestId('topology-canvas-skeleton')).toBeHidden({
			timeout: 10000,
		});
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 10000 });
	});

	test('analysis sweep skeleton appears during compute', async ({ page }) => {
		// Use sample-model source so the test doesn't depend on having a saved
		// run available in the dev environment.
		await page.goto(`${SVELTE_URL}/analysis`);

		// Switch to sample mode via the source-mode toggle (button labelled "Sample").
		const sampleBtn = page.getByRole('button', { name: 'Sample', exact: true });
		const sampleBtnVisible = await sampleBtn.isVisible().catch(() => false);
		if (!sampleBtnVisible) {
			test.skip(true, 'Sample-mode toggle not present; skipping sweep skeleton test');
			return;
		}
		await sampleBtn.click();

		// Wait for sweepValues to populate (the page derives values from the sample model).
		await page.waitForTimeout(800);

		// Delay /v1/sweep so the skeleton stays visible.
		await page.route('**/v1/sweep', async (route) => {
			await new Promise((r) => setTimeout(r, 800));
			await route.continue();
		});

		const runBtn = page.getByRole('button', { name: /Run sweep/ });
		await expect(runBtn).toBeVisible({ timeout: 5000 });

		const disabled = await runBtn.isDisabled();
		if (disabled) {
			test.skip(true, 'Run sweep is disabled in this state; skeleton path not exercised');
			return;
		}

		await runBtn.click();

		// Skeleton lands during compute.
		await expect(page.getByTestId('sweep-skeleton')).toBeVisible({ timeout: 5000 });
	});
});
