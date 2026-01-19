import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { loadConfig } from '../src/config.js';
import { createHandlers } from '../src/handlers.js';
import { DraftStore } from '../src/drafts.js';

const draftSimApiUrl = process.env.MCP_TEST_DRAFT_SIM_API_URL;
const engineApiUrl = process.env.MCP_TEST_ENGINE_API_URL;
const dataDir = process.env.MCP_TEST_DATA_DIR;
const templatesDir = process.env.MCP_TEST_TEMPLATES_DIR;
const draftsDir = process.env.MCP_TEST_DRAFT_TEMPLATES_DIR;

if (!draftSimApiUrl || !engineApiUrl || !dataDir || !templatesDir || !draftsDir) {
  test('draft integration tests skipped (missing MCP_TEST_DRAFT_SIM_API_URL or related env vars)', { skip: true }, () => {
    assert.ok(true);
  });
} else {
  const config = loadConfig({
    FLOWTIME_SIM_API_URL: process.env.MCP_TEST_SIM_API_URL ?? draftSimApiUrl,
    FLOWTIME_SIM_DRAFT_API_URL: draftSimApiUrl,
    FLOWTIME_API_URL: engineApiUrl,
    FLOWTIME_DATA_DIR: dataDir,
    FLOWTIME_TEMPLATES_DIR: templatesDir,
    FLOWTIME_TEMPLATES_DRAFT_DIR: draftsDir,
    MCP_REQUEST_TIMEOUT_MS: process.env.MCP_TEST_REQUEST_TIMEOUT_MS ?? '30000',
    MCP_ORCHESTRATION_TIMEOUT_MS: process.env.MCP_TEST_ORCHESTRATION_TIMEOUT_MS ?? '120000'
  });

  const handlers = createHandlers(config);
  const store = new DraftStore({ templatesDir, draftsDir });

  test('validateDraft returns warnings payload', async () => {
    const draft = await store.createDraft({ baseTemplateId: 'transportation-basic' });
    const result = await handlers.validateDraft({
      draftId: draft.metadata.draftId,
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
      draftId: draft.metadata.draftId,
      mode: 'simulation',
      parameters: {}
    });
    const payload = result.structuredContent as { runId?: string } | undefined;
    if (!payload?.runId) {
      throw new Error('Missing runId in structured payload.');
    }
  }, { timeout: 120000 });
}
