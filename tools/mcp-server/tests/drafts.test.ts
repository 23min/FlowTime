import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import os from 'node:os';
import fs from 'node:fs/promises';
import { parse as parseYaml } from 'yaml';
import { DraftStore } from '../src/drafts.js';

const createFixture = async () => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'flowtime-mcp-draft-'));
  const templatesDir = path.join(root, 'templates');
  const draftsDir = path.join(root, 'templates-draft');
  await fs.mkdir(templatesDir, { recursive: true });
  await fs.mkdir(draftsDir, { recursive: true });

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

  await fs.writeFile(path.join(templatesDir, 'base-template.yaml'), baseTemplate, 'utf8');

  return { root, templatesDir, draftsDir };
};

test('createDraft clones a template into templates-draft and rewrites metadata id', async () => {
  const { templatesDir, draftsDir } = await createFixture();
  const store = new DraftStore({ templatesDir, draftsDir });

  const record = await store.createDraft({ draftId: 'draft-one', baseTemplateId: 'base-template' });
  assert.equal(record.metadata.draftId, 'draft-one');
  assert.equal(record.metadata.baseTemplateId, 'base-template');

  const parsed = parseYaml(record.content) as { metadata?: { id?: string } };
  assert.equal(parsed?.metadata?.id, 'draft-one');

  const draftPath = path.join(draftsDir, 'draft-one.yaml');
  const metaPath = path.join(draftsDir, 'draft-one.meta.json');
  const draftExists = await fs.stat(draftPath).then(() => true, () => false);
  const metaExists = await fs.stat(metaPath).then(() => true, () => false);
  assert.ok(draftExists);
  assert.ok(metaExists);
});

test('applyDraftPatch enforces expected content hash', async () => {
  const { templatesDir, draftsDir } = await createFixture();
  const store = new DraftStore({ templatesDir, draftsDir });

  const record = await store.createDraft({ draftId: 'draft-two', baseTemplateId: 'base-template' });
  const updatedContent = `${record.content}\n# updated`;

  await assert.rejects(
    () => store.applyDraftPatch({ draftId: 'draft-two', content: updatedContent, expectedHash: 'sha256:bad' }),
    /hash mismatch/i
  );

  const patched = await store.applyDraftPatch({
    draftId: 'draft-two',
    content: updatedContent,
    expectedHash: record.metadata.contentHash
  });

  assert.notEqual(patched.metadata.contentHash, record.metadata.contentHash);
});

test('diffDraft returns a unified diff against the base template', async () => {
  const { templatesDir, draftsDir } = await createFixture();
  const store = new DraftStore({ templatesDir, draftsDir });

  await store.createDraft({ draftId: 'draft-three', baseTemplateId: 'base-template' });
  const diff = await store.diffDraft('draft-three');
  assert.ok(diff.diff.includes('base-template.yaml'));
  assert.ok(diff.diff.includes('draft-three.yaml'));
});

test('listDrafts returns draft metadata', async () => {
  const { templatesDir, draftsDir } = await createFixture();
  const store = new DraftStore({ templatesDir, draftsDir });

  await store.createDraft({ draftId: 'draft-four', baseTemplateId: 'base-template' });
  const drafts = await store.listDrafts();
  assert.equal(drafts.length, 1);
  assert.equal(drafts[0].draftId, 'draft-four');
});
