import test from 'node:test';
import assert from 'node:assert/strict';
import { getToolNames } from '../src/tools.js';

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
