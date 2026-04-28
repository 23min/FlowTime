import { test, expect } from '@playwright/test';

// m-E21-08 AC3 — bidirectional edge cross-link via new `selectedEdge` field
// on the shared view-state store.
//
// Contract:
//   - Edge selection is independent of edge pinning.
//   - .edge-pinned (renamed from prior .edge-selected) marks every pinned edge
//     in amber; .edge-selected marks the single selected edge in --ft-highlight
//     (turquoise) at heavier stroke weight.
//   - Topology edge click → pins + sets selectedEdge.
//   - Edge card body click → setSelectedEdge.
//   - Validation row edge click → pins + sets selectedEdge (covered indirectly;
//     this spec focuses on topology + edge-card paths).
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

test.describe('Edge bidirectional cross-link (m-E21-08 AC3)', () => {
	test.beforeEach(async ({}, testInfo) => {
		const infra = await infraUp();
		if (!infra.api || !infra.svelte) {
			testInfo.skip();
		}
	});

	test('clicking a topology edge pins it and applies both .edge-pinned and .edge-selected', async ({
		page,
	}) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		const firstEdgeHit = page.locator('[data-edge-hit="true"]').first();
		if ((await firstEdgeHit.count()) === 0) {
			test.skip(true, 'Model has no edges');
			return;
		}

		await firstEdgeHit.click({ force: true });

		// One edge card appears (the now-pinned edge).
		await expect(page.locator('button[aria-label*=" to "]').first()).toBeVisible({
			timeout: 5000,
		});

		// Both classes apply on the visible edge layer.
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-pinned'),
		).toHaveCount(1, { timeout: 5000 });
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-selected'),
		).toHaveCount(1, { timeout: 5000 });
	});

	test('clicking a different edge card body moves .edge-selected to that edge', async ({
		page,
	}) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		const edgeHits = page.locator('[data-edge-hit="true"]');
		const edgeCount = await edgeHits.count();
		if (edgeCount < 2) {
			test.skip(true, 'Model has fewer than 2 edges');
			return;
		}

		// Pin two edges so two cards exist.
		await edgeHits.nth(0).click({ force: true });
		await edgeHits.nth(1).click({ force: true });

		// Two edge cards.
		const edgeCards = page.locator('button[aria-label*=" to "]');
		await expect(edgeCards).toHaveCount(2, { timeout: 5000 });

		// After two pins, only the second is selected (last-clicked wins).
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-selected'),
		).toHaveCount(1);
		// Both are pinned.
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-pinned'),
		).toHaveCount(2);

		// Capture which edge is currently selected so we can verify it changes.
		const selectedFromBefore = await page
			.locator('[data-edge-from]:not([data-edge-hit]).edge-selected')
			.first()
			.getAttribute('data-edge-from');
		const selectedToBefore = await page
			.locator('[data-edge-from]:not([data-edge-hit]).edge-selected')
			.first()
			.getAttribute('data-edge-to');

		// Click the FIRST edge card body to move selection back to it. The
		// edge-card root carries role="button" (set when onSelect is provided);
		// walk up from the Unpin button to the card root.
		const firstCardRoot = edgeCards
			.first()
			.locator('xpath=ancestor::div[@role="button"][1]');
		await firstCardRoot.click();

		// Selection moved to a different edge.
		await expect
			.poll(
				async () => {
					const f = await page
						.locator('[data-edge-from]:not([data-edge-hit]).edge-selected')
						.first()
						.getAttribute('data-edge-from');
					const t = await page
						.locator('[data-edge-from]:not([data-edge-hit]).edge-selected')
						.first()
						.getAttribute('data-edge-to');
					return `${f}→${t}`;
				},
				{ timeout: 5000 },
			)
			.not.toBe(`${selectedFromBefore}→${selectedToBefore}`);

		// Still exactly one .edge-selected; both still .edge-pinned.
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-selected'),
		).toHaveCount(1);
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-pinned'),
		).toHaveCount(2);
	});

	test('clicking the selected pinned edge again unpins and clears selection', async ({
		page,
	}) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		const firstEdgeHit = page.locator('[data-edge-hit="true"]').first();
		if ((await firstEdgeHit.count()) === 0) {
			test.skip(true, 'Model has no edges');
			return;
		}

		// Pin + select.
		await firstEdgeHit.click({ force: true });
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-selected'),
		).toHaveCount(1, { timeout: 5000 });

		// Toggle off.
		await firstEdgeHit.click({ force: true });
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-selected'),
		).toHaveCount(0, { timeout: 5000 });
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-pinned'),
		).toHaveCount(0);
	});

	test('closing the selected edge card clears the selection', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		const firstEdgeHit = page.locator('[data-edge-hit="true"]').first();
		if ((await firstEdgeHit.count()) === 0) {
			test.skip(true, 'Model has no edges');
			return;
		}

		await firstEdgeHit.click({ force: true });
		const closeBtn = page.locator('button[aria-label^="Unpin "][aria-label*=" to "]').first();
		await expect(closeBtn).toBeVisible({ timeout: 5000 });

		await closeBtn.click();

		// Selection clears alongside the unpin.
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-selected'),
		).toHaveCount(0, { timeout: 5000 });
		await expect(
			page.locator('[data-edge-from]:not([data-edge-hit]).edge-pinned'),
		).toHaveCount(0);
	});
});
