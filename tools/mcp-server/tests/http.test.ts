import test from 'node:test';
import assert from 'node:assert/strict';
import { fetchJson, HttpError } from '../src/http.js';

const originalFetch = globalThis.fetch;

test.after(() => {
  globalThis.fetch = originalFetch;
});

test('fetchJson returns parsed JSON for ok responses', async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({ ok: true }), { status: 200 });
  const data = await fetchJson<{ ok: boolean }>('http://example.com', { method: 'GET' }, 1000);
  assert.equal(data.ok, true);
});

test('fetchJson throws HttpError for non-ok responses', async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({ error: 'bad' }), { status: 400 });
  await assert.rejects(
    () => fetchJson('http://example.com', { method: 'GET' }, 1000),
    (error: unknown) => {
      assert.ok(error instanceof HttpError);
      const typed = error as HttpError;
      assert.equal(typed.status, 400);
      return true;
    }
  );
});
