import { defineConfig, devices } from '@playwright/test';
import path from 'path';

const artifactsDir = path.join(process.cwd(), 'out', 'playwright-artifacts');

export default defineConfig({
  testDir: path.join(process.cwd(), 'tests', 'ui', 'specs'),
  outputDir: artifactsDir,
  timeout: 120000,
  expect: {
    timeout: 10000
  },
  reporter: [['list']],
  use: {
    baseURL: process.env.FLOWTIME_UI_BASE_URL || 'http://localhost:5219',
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
    screenshot: 'only-on-failure',
    viewport: { width: 1920, height: 1080 },
    userAgent: 'FlowTime-Playwright/FT-M-05.07'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ]
});
