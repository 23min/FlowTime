import { test, expect } from '@playwright/test';
import { getHoverDiagnostics, waitForTopologyReady, topologyCanvas } from '../helpers/flowtime-hud';

const RUN_ID = process.env.FLOWTIME_TEST_RUN_ID ?? 'run_20251214T151352Z_479f8f01';

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
});
