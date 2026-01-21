import test from 'node:test';
import assert from 'node:assert/strict';
import { getToolNames, registerTools, type ToolHandlers } from '../src/tools.js';
import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

test('tool list includes required PoC tools', () => {
  const names = getToolNames();
  const required = [
    'list_templates',
    'run_template',
    'get_run_summary',
    'get_graph',
    'get_state_window',
    'list_drafts',
    'create_draft',
    'get_draft',
    'apply_draft_patch',
    'diff_draft',
    'validate_draft',
    'generate_model',
    'run_draft',
    'ingest_series',
    'summarize_series',
    'fit_profile',
    'preview_profile',
    'map_series_to_inputs'
  ];
  for (const name of required) {
    assert.ok(names.includes(name), `missing tool ${name}`);
  }
});

test('get_state_window schema includes edge filters', () => {
  const configs = new Map<string, { inputSchema: Record<string, unknown> }>();
  const server = {
    registerTool: (name: string, config: { inputSchema: Record<string, unknown> }) => {
      configs.set(name, config);
    }
  };
  const emptyResult: CallToolResult = { content: [] };
  const handlers: ToolHandlers = {
    listTemplates: async () => emptyResult,
    runTemplate: async () => emptyResult,
    getRunSummary: async () => emptyResult,
    getGraph: async () => emptyResult,
    getStateWindow: async () => emptyResult,
    listDrafts: async () => emptyResult,
    createDraft: async () => emptyResult,
    getDraft: async () => emptyResult,
    applyDraftPatch: async () => emptyResult,
    diffDraft: async () => emptyResult,
    validateDraft: async () => emptyResult,
    generateModel: async () => emptyResult,
    runDraft: async () => emptyResult,
    ingestSeries: async () => emptyResult,
    summarizeSeries: async () => emptyResult,
    fitProfile: async () => emptyResult,
    previewProfile: async () => emptyResult,
    mapSeriesToInputs: async () => emptyResult
  };

  registerTools(server as unknown as Parameters<typeof registerTools>[0], handlers);

  const schema = configs.get('get_state_window')?.inputSchema;
  assert.ok(schema, 'missing get_state_window schema');
  assert.ok('edgeIds' in schema, 'edgeIds missing from get_state_window schema');
  assert.ok('edgeMetrics' in schema, 'edgeMetrics missing from get_state_window schema');
  assert.ok('classIds' in schema, 'classIds missing from get_state_window schema');
});
