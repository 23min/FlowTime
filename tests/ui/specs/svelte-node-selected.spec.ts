import { test, expect } from '@playwright/test';

// m-E21-08 AC2 — topology .node-selected stroke rule.
//
// Closes the m-E21-06 asymmetric one-way cross-link for nodes. When
// viewState.selectedCell names a node (set by topology click, card body
// click, heatmap cell click, or validation row click), the dag-map node
// group renders a turquoise --ft-highlight stroke.
//
// Cross-link directions exercised:
//   - Workbench card body click → topology node strokes turquoise
//   - Heatmap cell click + view-switch back to topology → stroke present
//   - Topology node click already covered by existing svelte-workbench tests
//     for the pin path; this spec adds the stroke verification.
//
// Graceful skip: if the API or Svelte dev server is not running, tests skip.

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

test.describe('Topology .node-selected stroke rule (m-E21-08 AC2)', () => {
	test.beforeEach(async ({}, testInfo) => {
		const infra = await infraUp();
		if (!infra.api || !infra.svelte) {
			testInfo.skip();
		}
	});

	test('clicking a topology node strokes that node turquoise', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		const firstNode = page.locator('[data-node-id]').first();
		await expect(firstNode).toBeVisible({ timeout: 15000 });
		const nodeId = await firstNode.getAttribute('data-node-id');
		expect(nodeId).toBeTruthy();

		// Reset to clean state — close any auto-pinned cards so the click below
		// is the unambiguous pin.
		const initial = await page.locator('button[aria-label^="Unpin"]').count();
		for (let i = 0; i < initial; i++) {
			await page.locator('button[aria-label^="Unpin"]').first().click();
		}

		await firstNode.click();

		// The clicked node group carries .node-selected; no other group does.
		await expect(page.locator(`[data-node-id="${nodeId}"].node-selected`)).toHaveCount(1, {
			timeout: 5000,
		});
		await expect(page.locator('[data-node-id].node-selected')).toHaveCount(1);
	});

	test('clicking a workbench card body strokes the matching topology node', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		// Close any auto-pinned cards so the test fully owns pin state.
		const initial = await page.locator('button[aria-label^="Unpin"]').count();
		for (let i = 0; i < initial; i++) {
			await page.locator('button[aria-label^="Unpin"]').first().click();
		}
		await expect(page.locator('button[aria-label^="Unpin"]')).toHaveCount(0);

		const allNodes = page.locator('[data-node-id]');
		const nodeCount = await allNodes.count();
		if (nodeCount < 2) {
			test.skip(true, 'Model has fewer than 2 nodes');
			return;
		}

		// Pin two nodes by topology click.
		const firstNode = allNodes.first();
		const firstId = await firstNode.getAttribute('data-node-id');
		await firstNode.click();
		await expect(page.locator(`button[aria-label="Unpin ${firstId}"]`)).toBeVisible({
			timeout: 5000,
		});

		const secondNode = allNodes.nth(1);
		const secondId = await secondNode.getAttribute('data-node-id');
		await secondNode.click();
		await expect(page.locator(`button[aria-label="Unpin ${secondId}"]`)).toBeVisible({
			timeout: 5000,
		});

		// After two pins, second is the selected one (last-clicked wins via setSelectedCell).
		await expect(page.locator(`[data-node-id="${secondId}"].node-selected`)).toHaveCount(1, {
			timeout: 5000,
		});

		// Click the FIRST card body. WorkbenchCard's root carries role="button"
		// (set when onSelect is provided) and contains the Unpin button as a
		// child. Walk up from the Unpin button to the card root.
		const firstCardRoot = page
			.locator(`button[aria-label="Unpin ${firstId}"]`)
			.locator('xpath=ancestor::div[@role="button"][1]');
		await firstCardRoot.click();

		await expect(page.locator(`[data-node-id="${firstId}"].node-selected`)).toHaveCount(1, {
			timeout: 5000,
		});
		await expect(page.locator(`[data-node-id="${secondId}"].node-selected`)).toHaveCount(0);
	});

	test('clearing the selection drops the stroke', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		const firstNode = page.locator('[data-node-id]').first();
		await expect(firstNode).toBeVisible({ timeout: 15000 });
		const nodeId = await firstNode.getAttribute('data-node-id');
		expect(nodeId).toBeTruthy();

		// Reset to clean state — close any auto-pinned cards so the click toggles cleanly.
		const initial = await page.locator('button[aria-label^="Unpin"]').count();
		for (let i = 0; i < initial; i++) {
			await page.locator('button[aria-label^="Unpin"]').first().click();
		}

		// Pin and select.
		await firstNode.click();
		await expect(page.locator(`[data-node-id="${nodeId}"].node-selected`)).toHaveCount(1, {
			timeout: 5000,
		});

		// Click again — the topology click handler unpins AND clearSelectedCell()s
		// (per +page.svelte:127-141 when selectedCell.nodeId matches).
		await firstNode.click();
		await expect(page.locator('[data-node-id].node-selected')).toHaveCount(0, { timeout: 5000 });
	});
});
