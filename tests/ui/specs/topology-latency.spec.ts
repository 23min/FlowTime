import { test, expect, Page } from '@playwright/test';
import { getHoverDiagnostics, waitForTopologyReady, topologyCanvas, getCanvasDiagnostics } from '../helpers/flowtime-hud';

const RUN_ID = process.env.FLOWTIME_TEST_RUN_ID ?? 'run_20251214T151352Z_479f8f01';

async function captureHoverDiagnostics(page: Page) {
  const canvas = topologyCanvas(page);
  await canvas.hover({ position: { x: 240, y: 240 } });
  await page.mouse.move(920, 360, { steps: 12 });
  await page.waitForTimeout(1500);
  return getHoverDiagnostics(page);
}

async function openInspectorPanel(page: Page) {
  const nodeProxy = page.locator('.topology-node-proxy').first();
  await nodeProxy.waitFor({ state: 'visible', timeout: 15000 });
  await nodeProxy.click();
  const toggle = page.locator('.topology-node-inspector-toggle');
  await toggle.waitFor({ state: 'visible', timeout: 15000 });
  await toggle.click();
  await page.waitForSelector('.topology-inspector', { timeout: 15000 });
}

test.describe('Topology latency budgets', () => {
  test('hover path exceeds target thresholds (RED)', async ({ page }) => {
    await page.goto(`/time-travel/topology?runId=${encodeURIComponent(RUN_ID)}&mode=simulation`);
    await waitForTopologyReady(page);

    const canvas = topologyCanvas(page);
    await canvas.click({ position: { x: 200, y: 200 } });

    await canvas.hover({ position: { x: 300, y: 300 } });
    await page.mouse.move(900, 400, { steps: 10 });
    await page.waitForTimeout(2000);

    const payload = await getHoverDiagnostics(page);
    expect(payload.pointerInpAverageMs ?? Infinity).toBeLessThanOrEqual(200);
    expect(payload.overlayUpdates ?? Infinity).toBeLessThanOrEqual(300);
  });

  test('inspector toggle keeps hover latency within budget', async ({ page }) => {
    await page.goto(`/time-travel/topology?runId=${encodeURIComponent(RUN_ID)}&mode=simulation`);
    await waitForTopologyReady(page);

    const baseline = await captureHoverDiagnostics(page);
    expect(baseline.inspectorVisible ?? false).toBeFalsy();
    expect(baseline.pointerInpAverageMs ?? Infinity).toBeLessThanOrEqual(200);
    expect(baseline.pointerQueueDrops ?? 0).toBeLessThanOrEqual(10);

    await openInspectorPanel(page);
    const inspectorStats = await captureHoverDiagnostics(page);
    expect(inspectorStats.inspectorVisible).toBeTruthy();
    expect(inspectorStats.pointerInpAverageMs ?? Infinity).toBeLessThanOrEqual(200);
    expect(inspectorStats.pointerQueueDrops ?? 0).toBeLessThanOrEqual(20);
    expect(inspectorStats.pointerEventsReceived ?? 0).toBeGreaterThan(0);
  });

  test('edge spatial index limits hover candidate count', async ({ page }) => {
    await page.goto(`/time-travel/topology?runId=${encodeURIComponent(RUN_ID)}&mode=simulation`);
    await waitForTopologyReady(page);

    const canvas = topologyCanvas(page);
    await canvas.hover({ position: { x: 260, y: 360 } });
    await page.mouse.move(960, 480, { steps: 14 });
    await page.waitForTimeout(1200);

    const stats = await getCanvasDiagnostics(page);
    expect(stats.edgeCandidatesLast ?? Infinity).toBeLessThanOrEqual(12);
    expect(stats.edgeCandidateFallbacks ?? 0).toBeLessThanOrEqual(1);
    expect(stats.edgeGridCellSize ?? 0).toBeGreaterThan(0);
  });
});
