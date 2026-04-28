import { test, expect } from '@playwright/test';

// m-E21-08 AC1 — topology keyboard navigation + ARIA structure.
//
// Brings the Svelte topology DAG to the heatmap's accessibility bar:
//   - every node + visible-edge carries tabindex/role/aria-label
//   - container is role="application" with descriptive aria-label
//   - Enter on a focused node pins it (mirrors mouse click)
//   - keyboard focus paints the --ft-focus chrome ring
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

test.describe('Topology a11y (m-E21-08 AC1)', () => {
	test.beforeEach(async ({}, testInfo) => {
		const infra = await infraUp();
		if (!infra.api || !infra.svelte) {
			testInfo.skip();
		}
	});

	test('nodes carry tabindex, role, and aria-label; container has role=application', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		const firstNode = page.locator('[data-node-id]').first();
		await expect(firstNode).toBeVisible({ timeout: 15000 });

		await expect(firstNode).toHaveAttribute('tabindex', '0');
		await expect(firstNode).toHaveAttribute('role', 'button');
		const label = await firstNode.getAttribute('aria-label');
		expect(label).toBeTruthy();
		// Shape: "<id> [(<class>)] — <metric>: <value | "no data">"
		expect(label).toMatch(/—.+:/);

		const container = page.locator('[role="application"]').first();
		await expect(container).toHaveAttribute('aria-label', /Topology graph/);
	});

	test('visible edges carry tabindex, role, and aria-label', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		await expect(page.locator('[data-node-id]').first()).toBeVisible({ timeout: 15000 });

		const firstEdge = page.locator('[data-edge-from]:not([data-edge-hit])').first();
		if ((await firstEdge.count()) === 0) {
			test.skip(true, 'Model has no edges');
			return;
		}

		await expect(firstEdge).toHaveAttribute('tabindex', '0');
		await expect(firstEdge).toHaveAttribute('role', 'button');
		const label = await firstEdge.getAttribute('aria-label');
		expect(label).toMatch(/^edge from .+ to .+$/);
	});

	test('Enter on a focused node pins the node (matches mouse click)', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		const firstNode = page.locator('[data-node-id]').first();
		await expect(firstNode).toBeVisible({ timeout: 15000 });

		// Auto-pin runs on load — close any existing cards so we can detect the
		// keyboard-driven pin unambiguously.
		const initialCount = await page.locator('button[aria-label^="Unpin"]').count();
		for (let i = 0; i < initialCount; i++) {
			await page.locator('button[aria-label^="Unpin"]').first().click();
		}
		await expect(page.locator('button[aria-label^="Unpin"]')).toHaveCount(0);

		const nodeId = await firstNode.getAttribute('data-node-id');
		expect(nodeId).toBeTruthy();

		await firstNode.focus();
		await page.keyboard.press('Enter');

		await expect(
			page.locator(`button[aria-label="Unpin ${nodeId}"]`),
		).toBeVisible({ timeout: 5000 });
	});

	test('focus paints --ft-focus chrome ring on the focused node', async ({ page }) => {
		await page.goto(`${SVELTE_URL}/time-travel/topology`);
		const firstNode = page.locator('[data-node-id]').first();
		await expect(firstNode).toBeVisible({ timeout: 15000 });

		// Wait for the a11y attribute injection to land — the post-render
		// $effect runs after the dag-map SVG renders. tabindex on the node is
		// the load-bearing signal that the effect has completed.
		await expect(firstNode).toHaveAttribute('tabindex', '0', { timeout: 5000 });

		// Drive a keyboard-style focus path so :focus-visible matches.
		await page.evaluate(() => {
			const el = document.querySelector('[data-node-id]') as HTMLElement | null;
			el?.focus();
		});
		await page.keyboard.press('Tab');
		await page.keyboard.press('Shift+Tab');

		// Outline-style is "solid" per the CSS rule when :focus-visible matches.
		// Some browsers report "auto" (UA stylesheet fallback).
		await expect.poll(
			async () => firstNode.evaluate((el) => getComputedStyle(el).outlineStyle),
			{ timeout: 5000 },
		).toMatch(/^(solid|auto)$/);
	});
});
