import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { loadConfig } from '../src/config.js';
import { createHandlers } from '../src/handlers.js';
import { DraftStore } from '../src/drafts.js';

const draftSimApiUrl = process.env.MCP_TEST_DRAFT_SIM_API_URL;
const engineApiUrl = process.env.MCP_TEST_ENGINE_API_URL;
if (!draftSimApiUrl || !engineApiUrl) {
  test('draft integration tests skipped (missing MCP_TEST_DRAFT_SIM_API_URL or MCP_TEST_ENGINE_API_URL)', { skip: true }, () => {
    assert.ok(true);
  });
} else {
  const config = loadConfig({
    FLOWTIME_SIM_API_URL: process.env.MCP_TEST_SIM_API_URL ?? draftSimApiUrl,
    FLOWTIME_SIM_DRAFT_API_URL: draftSimApiUrl,
    FLOWTIME_API_URL: engineApiUrl,
    MCP_REQUEST_TIMEOUT_MS: process.env.MCP_TEST_REQUEST_TIMEOUT_MS ?? '30000',
    MCP_ORCHESTRATION_TIMEOUT_MS: process.env.MCP_TEST_ORCHESTRATION_TIMEOUT_MS ?? '120000'
  });

  const handlers = createHandlers(config);
  const store = new DraftStore({ simApiUrl: draftSimApiUrl, requestTimeoutMs: 30000 });

  test('validateDraft returns warnings payload', async () => {
    const draft = await store.createDraft({ baseTemplateId: 'transportation-basic' });
    const result = await handlers.validateDraft({
      draftId: draft.draftId,
      parameters: {},
      mode: 'simulation'
    });
    const payload = result.structuredContent as { warnings?: unknown[] } | undefined;
    if (!payload) {
      throw new Error('Missing structured payload.');
    }
    assert.ok(Array.isArray(payload.warnings));
  }, { timeout: 120000 });

  test('runDraft returns a runId', async () => {
    const draft = await store.createDraft({ baseTemplateId: 'transportation-basic' });
    const result = await handlers.runDraft({
      draftId: draft.draftId,
      mode: 'simulation',
      parameters: {}
    });
    const payload = result.structuredContent as { runId?: string } | undefined;
    if (!payload?.runId) {
      throw new Error('Missing runId in structured payload.');
    }
  }, { timeout: 120000 });

  test('draft run can be inspected via getRunSummary', async () => {
    const draft = await store.createDraft({ baseTemplateId: 'transportation-basic' });
    const runResult = await handlers.runDraft({
      draftId: draft.draftId,
      mode: 'simulation',
      parameters: {}
    });
    const payload = runResult.structuredContent as { runId?: string } | undefined;
    if (!payload?.runId) {
      throw new Error('Missing runId in structured payload.');
    }

    const summaryResult = await handlers.getRunSummary({ runId: payload.runId });
    const summaryPayload = summaryResult.structuredContent as { summary?: unknown } | undefined;
    if (!summaryPayload?.summary) {
      throw new Error('Missing summary in structured payload.');
    }
  }, { timeout: 120000 });
}
