import { Page, Locator } from '@playwright/test';

export type HoverDiagnosticsPayload = {
  pointerInpAverageMs?: number;
  pointerInpMaxMs?: number;
  pointerInpSampleCount?: number;
  overlayUpdates?: number;
  sceneRebuilds?: number;
  layoutReads?: number;
  pointerEventsReceived?: number;
  pointerQueueDrops?: number;
  dragTotalDurationMs?: number;
  dragFrameCount?: number;
  timestampUtc?: string;
  source?: string;
  zoomPercent?: number;
  mode?: string;
  runId?: string;
};

export async function waitForTopologyReady(page: Page) {
  await page.waitForSelector('canvas[data-topology-canvas]', { timeout: 60000 });
  await page.waitForSelector('[data-test="topology-loaded"]', { timeout: 60000 }).catch(() => { /* optional marker */ });
}

export async function getHoverDiagnostics(page: Page): Promise<HoverDiagnosticsPayload> {
  return page.evaluate(() => {
    const canvas = document.querySelector('canvas[data-topology-canvas]');
    if (!canvas || !(window as any).FlowTime?.TopologyCanvas) {
      throw new Error('Topology canvas not ready');
    }
    return (window as any).FlowTime.TopologyCanvas.dumpHoverDiagnostics(canvas);
  });
}

export async function getCanvasDiagnostics(page: Page) {
  return page.evaluate(() => {
    const canvas = document.querySelector('canvas[data-topology-canvas]');
    if (!canvas || !(window as any).FlowTime?.TopologyCanvas) {
      throw new Error('Topology canvas not ready');
    }
    return (window as any).FlowTime.TopologyCanvas.getCanvasDiagnostics(canvas, 'playwright');
  });
}

export function topologyCanvas(page: Page): Locator {
  return page.locator('canvas[data-topology-canvas]');
}
