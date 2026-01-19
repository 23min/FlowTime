import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash, randomUUID } from 'node:crypto';
import { createTwoFilesPatch } from 'diff';
import { parse as parseYaml, stringify as stringifyYaml } from 'yaml';

export type DraftMetadata = {
  draftId: string;
  baseTemplateId?: string;
  createdAt: string;
  updatedAt: string;
  contentHash: string;
};

export type DraftSummary = {
  draftId: string;
  baseTemplateId?: string;
  updatedAt: string;
  contentHash: string;
};

export type DraftRecord = {
  metadata: DraftMetadata;
  content: string;
};

export type DraftCreateOptions = {
  draftId?: string;
  baseTemplateId?: string;
  content?: string;
};

export type DraftPatchOptions = {
  draftId: string;
  content: string;
  expectedHash?: string;
};

export type DraftDiff = {
  draftId: string;
  baseTemplateId?: string;
  diff: string;
};

type DraftStoreOptions = {
  templatesDir: string;
  draftsDir: string;
};

const SAFE_ID = /^[A-Za-z0-9][A-Za-z0-9._-]*$/;

const nowIso = (): string => new Date().toISOString();

const computeHash = (content: string): string =>
  `sha256:${createHash('sha256').update(content).digest('hex')}`;

const ensureSafeId = (id: string, label: string): void => {
  if (!SAFE_ID.test(id)) {
    throw new Error(`${label} must contain only letters, numbers, '.', '_' or '-'.`);
  }
};

const buildDraftFile = (draftsDir: string, draftId: string): string =>
  path.join(draftsDir, `${draftId}.yaml`);

const buildMetaFile = (draftsDir: string, draftId: string): string =>
  path.join(draftsDir, `${draftId}.meta.json`);

const ensureDir = async (dir: string): Promise<void> => {
  await fs.mkdir(dir, { recursive: true });
};

const loadYamlTemplate = async (filePath: string): Promise<string> => {
  const content = await fs.readFile(filePath, 'utf8');
  if (!content.trim()) {
    throw new Error(`Template file ${filePath} is empty.`);
  }
  return content;
};

const setMetadataId = (content: string, draftId: string): string => {
  const doc = parseYaml(content) as Record<string, unknown> | null;
  if (!doc || typeof doc !== 'object') {
    throw new Error('Template YAML must be a mapping at the root.');
  }
  const metadata = (doc.metadata && typeof doc.metadata === 'object')
    ? (doc.metadata as Record<string, unknown>)
    : {};
  metadata.id = draftId;
  doc.metadata = metadata;
  return stringifyYaml(doc);
};

export class DraftStore {
  private readonly templatesDir: string;
  private readonly draftsDir: string;

  constructor(options: DraftStoreOptions) {
    this.templatesDir = options.templatesDir;
    this.draftsDir = options.draftsDir;
  }

  async listDrafts(): Promise<DraftSummary[]> {
    await ensureDir(this.draftsDir);
    const entries = await fs.readdir(this.draftsDir, { withFileTypes: true });
    const drafts: DraftSummary[] = [];

    for (const entry of entries) {
      if (!entry.isFile() || !entry.name.endsWith('.yaml')) {
        continue;
      }
      const draftId = entry.name.replace(/\.yaml$/, '');
      const meta = await this.readMetadata(draftId);
      const content = await fs.readFile(buildDraftFile(this.draftsDir, draftId), 'utf8');
      const contentHash = computeHash(content);
      const updatedAt = meta?.updatedAt ?? (await fs.stat(buildDraftFile(this.draftsDir, draftId))).mtime.toISOString();
      drafts.push({
        draftId,
        baseTemplateId: meta?.baseTemplateId,
        updatedAt,
        contentHash
      });
    }

    return drafts.sort((a, b) => a.draftId.localeCompare(b.draftId));
  }

  async getDraft(draftId: string): Promise<DraftRecord> {
    ensureSafeId(draftId, 'draftId');
    const content = await fs.readFile(buildDraftFile(this.draftsDir, draftId), 'utf8');
    const contentHash = computeHash(content);
    const meta = await this.readMetadata(draftId);
    const now = nowIso();
    const metadata: DraftMetadata = {
      draftId,
      baseTemplateId: meta?.baseTemplateId,
      createdAt: meta?.createdAt ?? now,
      updatedAt: meta?.updatedAt ?? now,
      contentHash
    };
    return { metadata, content };
  }

  async createDraft(options: DraftCreateOptions): Promise<DraftRecord> {
    const draftId = options.draftId ?? `draft-${randomUUID()}`;
    ensureSafeId(draftId, 'draftId');
    await ensureDir(this.draftsDir);

    const draftPath = buildDraftFile(this.draftsDir, draftId);
    try {
      await fs.access(draftPath);
      throw new Error(`Draft '${draftId}' already exists.`);
    } catch {
      // ok
    }

    let content = '';
    if (options.content) {
      content = setMetadataId(options.content, draftId);
    } else if (options.baseTemplateId) {
      ensureSafeId(options.baseTemplateId, 'baseTemplateId');
      const basePath = path.join(this.templatesDir, `${options.baseTemplateId}.yaml`);
      const baseContent = await loadYamlTemplate(basePath);
      content = setMetadataId(baseContent, draftId);
    } else {
      content = stringifyYaml({
        schemaVersion: 1,
        generator: 'flowtime-sim',
        metadata: {
          id: draftId,
          title: `Draft ${draftId}`,
          description: 'Draft template created via MCP.'
        },
        window: {
          start: new Date().toISOString(),
          timezone: 'UTC'
        },
        parameters: [],
        topology: {
          nodes: []
        }
      });
    }

    const contentHash = computeHash(content);
    const timestamp = nowIso();
    const metadata: DraftMetadata = {
      draftId,
      baseTemplateId: options.baseTemplateId,
      createdAt: timestamp,
      updatedAt: timestamp,
      contentHash
    };

    await fs.writeFile(draftPath, content, 'utf8');
    await this.writeMetadata(metadata);

    return { metadata, content };
  }

  async applyDraftPatch(options: DraftPatchOptions): Promise<DraftRecord> {
    ensureSafeId(options.draftId, 'draftId');
    const draftPath = buildDraftFile(this.draftsDir, options.draftId);
    const current = await fs.readFile(draftPath, 'utf8');
    const currentHash = computeHash(current);

    if (options.expectedHash && options.expectedHash !== currentHash) {
      throw new Error('Draft content hash mismatch. Reload and retry.');
    }

    const content = options.content;
    const contentHash = computeHash(content);
    await fs.writeFile(draftPath, content, 'utf8');

    const meta = await this.readMetadata(options.draftId);
    const metadata: DraftMetadata = {
      draftId: options.draftId,
      baseTemplateId: meta?.baseTemplateId,
      createdAt: meta?.createdAt ?? nowIso(),
      updatedAt: nowIso(),
      contentHash
    };
    await this.writeMetadata(metadata);
    return { metadata, content };
  }

  async diffDraft(draftId: string): Promise<DraftDiff> {
    ensureSafeId(draftId, 'draftId');
    const record = await this.getDraft(draftId);
    const baseTemplateId = record.metadata.baseTemplateId;
    let baseContent = '';
    if (baseTemplateId) {
      ensureSafeId(baseTemplateId, 'baseTemplateId');
      baseContent = await loadYamlTemplate(path.join(this.templatesDir, `${baseTemplateId}.yaml`));
    }
    const diff = createTwoFilesPatch(
      baseTemplateId ? `${baseTemplateId}.yaml` : 'base.yaml',
      `${draftId}.yaml`,
      baseContent,
      record.content,
      '',
      '',
      { context: 3 }
    );
    return { draftId, baseTemplateId, diff };
  }

  private async readMetadata(draftId: string): Promise<DraftMetadata | undefined> {
    const metaPath = buildMetaFile(this.draftsDir, draftId);
    try {
      const raw = await fs.readFile(metaPath, 'utf8');
      return JSON.parse(raw) as DraftMetadata;
    } catch {
      return undefined;
    }
  }

  private async writeMetadata(metadata: DraftMetadata): Promise<void> {
    await fs.writeFile(
      buildMetaFile(this.draftsDir, metadata.draftId),
      JSON.stringify(metadata, null, 2),
      'utf8'
    );
  }
}
