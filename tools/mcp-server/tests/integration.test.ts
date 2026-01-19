import test from 'node:test';
import assert from 'node:assert/strict';
import { loadConfig } from '../src/config.js';
import { createHandlers } from '../src/handlers.js';

const simApiUrl = process.env.MCP_TEST_SIM_API_URL;
const engineApiUrl = process.env.MCP_TEST_ENGINE_API_URL;

if (!simApiUrl || !engineApiUrl) {
  test('integration tests skipped (missing MCP_TEST_SIM_API_URL or MCP_TEST_ENGINE_API_URL)', { skip: true }, () => {
    assert.ok(true);
  });
} else {
  const config = loadConfig({
    FLOWTIME_SIM_API_URL: simApiUrl,
    FLOWTIME_API_URL: engineApiUrl,
    FLOWTIME_DATA_DIR: process.env.MCP_TEST_DATA_DIR ?? 'data',
    MCP_REQUEST_TIMEOUT_MS: process.env.MCP_TEST_REQUEST_TIMEOUT_MS ?? '30000',
    MCP_ORCHESTRATION_TIMEOUT_MS: process.env.MCP_TEST_ORCHESTRATION_TIMEOUT_MS ?? '120000'
  });

  const handlers = createHandlers(config);

  test('listTemplates returns a templates payload', async () => {
    const result = await handlers.listTemplates();
    const payload = result.structuredContent as { templates?: unknown[] } | undefined;
    if (!payload) {
      throw new Error('Missing structured payload.');
    }
    assert.ok(Array.isArray(payload.templates));
  });

  const templateId = process.env.MCP_TEST_TEMPLATE_ID;
  if (!templateId) {
    test('runTemplate skipped (missing MCP_TEST_TEMPLATE_ID)', { skip: true }, () => {
      assert.ok(true);
    });
  } else {
    test('runTemplate returns a runId', { timeout: 120000 }, async () => {
      const rngSeed = process.env.MCP_TEST_RNG_SEED
        ? Number.parseInt(process.env.MCP_TEST_RNG_SEED, 10)
        : undefined;
      const result = await handlers.runTemplate({
        templateId,
        mode: 'simulation',
        parameters: {},
        rngSeed
      });
      const payload = result.structuredContent as { runId?: string } | undefined;
      if (!payload?.runId) {
        throw new Error('Missing runId in structured payload.');
      }
    });
  }
}
