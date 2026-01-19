import test from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import { parse as parseYaml } from 'yaml';
import { createHash } from 'node:crypto';
import { DraftStore } from '../src/drafts.js';

const computeHash = (content: string): string =>
  `sha256:${createHash('sha256').update(content).digest('hex')}`;

const createFixtureServer = async () => {
  const drafts = new Map<string, { content: string; metadata?: Record<string, string>; updatedUtc: string }>();
  const baseTemplate = [
    'schemaVersion: 1',
    'generator: flowtime-sim',
    'metadata:',
    '  id: base-template',
    '  title: Base',
    'window:',
    '  start: 2025-01-01T00:00:00Z',
    '  timezone: UTC',
    'parameters: []',
    'topology:',
    '  nodes: []'
  ].join('\n');

  const server = http.createServer(async (req, res) => {
    const url = new URL(req.url ?? '/', 'http://localhost');
    if (req.method === 'GET' && url.pathname === '/api/v1/templates/base-template/source') {
      res.writeHead(200, { 'content-type': 'application/json' });
      res.end(JSON.stringify({ id: 'base-template', source: baseTemplate }));
      return;
    }

    if (req.method === 'GET' && url.pathname === '/api/v1/drafts') {
      const items = Array.from(drafts.entries()).map(([draftId, entry]) => ({
        draftId,
        contentHash: computeHash(entry.content),
        updatedUtc: entry.updatedUtc,
        metadata: entry.metadata
      }));
      res.writeHead(200, { 'content-type': 'application/json' });
      res.end(JSON.stringify({ items }));
      return;
    }

    const draftMatch = url.pathname.match(/^\/api\/v1\/drafts\/([^/]+)$/);
    if (draftMatch && req.method === 'GET') {
      const draftId = draftMatch[1];
      const entry = drafts.get(draftId);
      if (!entry) {
        res.writeHead(404, { 'content-type': 'application/json' });
        res.end(JSON.stringify({ error: 'not found' }));
        return;
      }
      res.writeHead(200, { 'content-type': 'application/json' });
      res.end(JSON.stringify({
        draftId,
        content: entry.content,
        contentHash: computeHash(entry.content),
        metadata: entry.metadata
      }));
      return;
    }

    if (req.method === 'POST' && url.pathname === '/api/v1/drafts') {
      let body = '';
      req.on('data', (chunk) => { body += chunk; });
      req.on('end', () => {
        const payload = JSON.parse(body);
        const draftId = payload.draftId;
        drafts.set(draftId, {
          content: payload.content,
          metadata: payload.metadata,
          updatedUtc: new Date().toISOString()
        });
        res.writeHead(201, { 'content-type': 'application/json' });
        res.end(JSON.stringify({
          draftId,
          storageRef: `storage://draft/${draftId}`,
          contentHash: computeHash(payload.content)
        }));
      });
      return;
    }

    if (draftMatch && req.method === 'PUT') {
      let body = '';
      req.on('data', (chunk) => { body += chunk; });
      req.on('end', () => {
        const draftId = draftMatch[1];
        const payload = JSON.parse(body);
        drafts.set(draftId, {
          content: payload.content,
          metadata: payload.metadata,
          updatedUtc: new Date().toISOString()
        });
        res.writeHead(200, { 'content-type': 'application/json' });
        res.end(JSON.stringify({
          draftId,
          storageRef: `storage://draft/${draftId}`,
          contentHash: computeHash(payload.content)
        }));
      });
      return;
    }

    res.writeHead(404, { 'content-type': 'application/json' });
    res.end(JSON.stringify({ error: 'not found' }));
  });

  await new Promise<void>((resolve) => {
    server.listen(0, resolve);
  });

  const address = server.address();
  if (!address || typeof address === 'string') {
    throw new Error('Failed to bind test server.');
  }

  const baseUrl = `http://127.0.0.1:${address.port}/api/v1`;
  return {
    baseUrl,
    close: () => server.close()
  };
};

test('createDraft clones a template via HTTP and rewrites metadata id', async () => {
  const fixture = await createFixtureServer();
  const store = new DraftStore({ simApiUrl: fixture.baseUrl, requestTimeoutMs: 5000 });

  const record = await store.createDraft({ draftId: 'draft-one', baseTemplateId: 'base-template' });
  assert.equal(record.draftId, 'draft-one');
  assert.equal(record.metadata?.baseTemplateId, 'base-template');

  const parsed = parseYaml(record.content) as { metadata?: { id?: string } };
  assert.equal(parsed?.metadata?.id, 'draft-one');

  fixture.close();
});

test('applyDraftPatch enforces expected content hash', async () => {
  const fixture = await createFixtureServer();
  const store = new DraftStore({ simApiUrl: fixture.baseUrl, requestTimeoutMs: 5000 });

  const record = await store.createDraft({ draftId: 'draft-two', baseTemplateId: 'base-template' });
  const updatedContent = `${record.content}\n# updated`;

  await assert.rejects(
    () => store.applyDraftPatch({ draftId: 'draft-two', content: updatedContent, expectedHash: 'sha256:bad' }),
    /hash mismatch/i
  );

  const patched = await store.applyDraftPatch({
    draftId: 'draft-two',
    content: updatedContent,
    expectedHash: computeHash(record.content)
  });

  assert.notEqual(patched.contentHash, record.contentHash);
  fixture.close();
});

test('diffDraft returns a unified diff against the base template', async () => {
  const fixture = await createFixtureServer();
  const store = new DraftStore({ simApiUrl: fixture.baseUrl, requestTimeoutMs: 5000 });

  await store.createDraft({ draftId: 'draft-three', baseTemplateId: 'base-template' });
  const diff = await store.diffDraft('draft-three');
  assert.ok(diff.diff.includes('base-template.yaml'));
  assert.ok(diff.diff.includes('draft-three.yaml'));

  fixture.close();
});

test('listDrafts returns draft metadata', async () => {
  const fixture = await createFixtureServer();
  const store = new DraftStore({ simApiUrl: fixture.baseUrl, requestTimeoutMs: 5000 });

  await store.createDraft({ draftId: 'draft-four', baseTemplateId: 'base-template' });
  const drafts = await store.listDrafts();
  assert.equal(drafts.length, 1);
  assert.equal(drafts[0].draftId, 'draft-four');

  fixture.close();
});
