import test from 'node:test';
import assert from 'node:assert/strict';
import { loadConfig } from '../src/config.js';

test('loadConfig uses defaults when env is empty', () => {
  const config = loadConfig({});
  assert.equal(config.simApiUrl, 'http://localhost:8090/api/v1');
  assert.equal(config.draftSimApiUrl, 'http://localhost:8090/api/v1');
  assert.equal(config.engineApiUrl, 'http://localhost:8080/v1');
  assert.equal(config.maxBins, 1000);
  assert.equal(config.requestTimeoutMs, 30000);
  assert.equal(config.orchestrationTimeoutMs, 120000);
});

test('loadConfig throws on invalid numeric values', () => {
  assert.throws(() => {
    loadConfig({ MCP_MAX_BINS: 'abc' });
  }, /MCP_MAX_BINS/);
});

test('loadConfig throws on invalid URLs', () => {
  assert.throws(() => {
    loadConfig({ FLOWTIME_SIM_API_URL: 'not-a-url' });
  }, /FLOWTIME_SIM_API_URL/);
});
