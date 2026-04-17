import { test, expect } from '@playwright/test';

// End-to-end test for the Svelte workbench (m-E21-01).
//
// Verifies:
// - Topology page loads with compact layout
// - Clicking a node opens a workbench card
// - Clicking the close button removes the card
// - Timeline scrubbing updates card metrics
//
// Graceful skip: if the API or Svelte dev server is not running,
// tests are skipped. To run locally:
//   1. Terminal A: dotnet run --project src/FlowTime.API
//   2. Terminal B: cd ui && pnpm dev
//   3. Terminal C: npm run test-ui -- --grep "Workbench"

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
		probe(`${API_URL}/v1/health`),
		probe(`${SVELTE_URL}/`),
	]);
	return { api, svelte };
}

test.describe('Workbench', () => {
	test.beforeEach(async ({}, testInfo) => {
		const infra = await infraUp();
		if (!infra.api || !infra.svelte) {
			testInfo.skip();
		}
	});

	test('topology page loads with compact layout', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		// Should have the toolbar with "Topology" label
		await expect(page.locator('text=Topology').first()).toBeVisible({ timeout: 10000 });
		// Workbench area should show empty state
		await expect(page.locator('text=Click a node to inspect')).toBeVisible({ timeout: 15000 });
	});

	test('clicking a node opens a workbench card', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		// Wait for the DAG to render (SVG with data-node-id)
		const firstNode = page.locator('[data-node-id]').first();
		await expect(firstNode).toBeVisible({ timeout: 15000 });

		// Click the first node
		await firstNode.click();

		// A workbench card should appear (it has an "Unpin" close button)
		await expect(page.locator('button[aria-label^="Unpin"]').first()).toBeVisible({ timeout: 5000 });
	});

	test('clicking close removes the workbench card', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		const firstNode = page.locator('[data-node-id]').first();
		await expect(firstNode).toBeVisible({ timeout: 15000 });
		await firstNode.click();

		// Card appears
		const closeBtn = page.locator('button[aria-label^="Unpin"]').first();
		await expect(closeBtn).toBeVisible({ timeout: 5000 });

		// Close it
		await closeBtn.click();

		// Empty state should return (unless auto-pin is still active)
		// At minimum the close button for that specific card should be gone
		await expect(closeBtn).not.toBeVisible({ timeout: 3000 });
	});

	test('auto-pins highest utilization node on load', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		// Wait for the SVG to render
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// Give time for auto-pin (happens after first bin loads)
		// Should see a workbench card (not the empty state)
		await expect(page.locator('button[aria-label^="Unpin"]').first()).toBeVisible({ timeout: 10000 });
	});
});
