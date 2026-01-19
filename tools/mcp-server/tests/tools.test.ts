import test from 'node:test';
import assert from 'node:assert/strict';
import { getToolNames } from '../src/tools.js';

test('tool list includes required PoC tools', () => {
  const names = getToolNames();
  const required = ['list_templates', 'run_template', 'get_run_summary', 'get_graph', 'get_state_window'];
  for (const name of required) {
    assert.ok(names.includes(name), `missing tool ${name}`);
  }
});
