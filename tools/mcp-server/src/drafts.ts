import { createHash, randomUUID } from 'node:crypto';
import { createTwoFilesPatch } from 'diff';
import { parse as parseYaml, stringify as stringifyYaml } from 'yaml';
import { fetchJson } from './http.js';

export type DraftMetadata = {
  draftId: string;
  baseTemplateId?: string;
  createdAt?: string;
  updatedAt?: string;
  contentHash?: string;
};

export type DraftSummary = {
  draftId: string;
  baseTemplateId?: string;
  updatedAt?: string;
  contentHash?: string;
};

export type DraftRecord = {
  draftId: string;
  storageRef?: string;
  content: string;
  contentHash?: string;
  metadata?: Record<string, string>;
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
  simApiUrl: string;
  requestTimeoutMs: number;
};

const SAFE_ID = /^[A-Za-z0-9][A-Za-z0-9._-]*$/;

const computeHash = (content: string): string =>
  `sha256:${createHash('sha256').update(content).digest('hex')}`;

const ensureSafeId = (id: string, label: string): void => {
  if (!SAFE_ID.test(id)) {
    throw new Error(`${label} must contain only letters, numbers, '.', '_' or '-'.`);
  }
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

const buildDefaultDraft = (draftId: string): string =>
  stringifyYaml({
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

export class DraftStore {
  private readonly simApiUrl: string;
  private readonly requestTimeoutMs: number;

  constructor(options: DraftStoreOptions) {
    this.simApiUrl = options.simApiUrl;
    this.requestTimeoutMs = options.requestTimeoutMs;
  }

  async listDrafts(): Promise<DraftSummary[]> {
    const response = await fetchJson<{ items?: Array<Record<string, unknown>> }>(
      `${this.simApiUrl}/drafts`,
      { method: 'GET' },
      this.requestTimeoutMs
    );

    const items = response.items ?? [];
    return items.map((item) => {
      const metadata = (item.metadata ?? {}) as Record<string, string>;
      return {
        draftId: String(item.draftId ?? ''),
        baseTemplateId: metadata.baseTemplateId,
        updatedAt: typeof item.updatedUtc === 'string' ? item.updatedUtc : undefined,
        contentHash: typeof item.contentHash === 'string' ? item.contentHash : undefined
      };
    });
  }

  async getDraft(draftId: string): Promise<DraftRecord> {
    ensureSafeId(draftId, 'draftId');
    const response = await fetchJson<Record<string, unknown>>(
      `${this.simApiUrl}/drafts/${draftId}`,
      { method: 'GET' },
      this.requestTimeoutMs
    );

    return {
      draftId: String(response.draftId ?? draftId),
      storageRef: typeof response.storageRef === 'string' ? response.storageRef : undefined,
      content: String(response.content ?? ''),
      contentHash: typeof response.contentHash === 'string' ? response.contentHash : undefined,
      metadata: (response.metadata ?? undefined) as Record<string, string> | undefined
    };
  }

  async createDraft(options: DraftCreateOptions): Promise<DraftRecord> {
    const draftId = options.draftId ?? `draft-${randomUUID()}`;
    ensureSafeId(draftId, 'draftId');

    let content = options.content;
    if (!content && options.baseTemplateId) {
      ensureSafeId(options.baseTemplateId, 'baseTemplateId');
      const template = await fetchJson<{ source?: string }>(
        `${this.simApiUrl}/templates/${options.baseTemplateId}/source`,
        { method: 'GET' },
        this.requestTimeoutMs
      );
      if (!template.source) {
        throw new Error(`Template '${options.baseTemplateId}' source was empty.`);
      }
      content = template.source;
    }

    const payloadContent = setMetadataId(content ?? buildDefaultDraft(draftId), draftId);
    const metadata: Record<string, string> = {};
    if (options.baseTemplateId) {
      metadata.baseTemplateId = options.baseTemplateId;
    }

    await fetchJson(
      `${this.simApiUrl}/drafts`,
      {
        method: 'POST',
        body: JSON.stringify({
          draftId,
          content: payloadContent,
          overwrite: false,
          metadata: Object.keys(metadata).length > 0 ? metadata : undefined
        })
      },
      this.requestTimeoutMs
    );

    return this.getDraft(draftId);
  }

  async applyDraftPatch(options: DraftPatchOptions): Promise<DraftRecord> {
    ensureSafeId(options.draftId, 'draftId');
    const current = await this.getDraft(options.draftId);
    const currentHash = computeHash(current.content);

    if (options.expectedHash && options.expectedHash !== currentHash) {
      throw new Error('Draft content hash mismatch. Reload and retry.');
    }

    await fetchJson(
      `${this.simApiUrl}/drafts/${options.draftId}`,
      {
        method: 'PUT',
        body: JSON.stringify({
          content: options.content,
          metadata: current.metadata
        })
      },
      this.requestTimeoutMs
    );

    return this.getDraft(options.draftId);
  }

  async diffDraft(draftId: string): Promise<DraftDiff> {
    ensureSafeId(draftId, 'draftId');
    const record = await this.getDraft(draftId);
    const metadata = record.metadata ?? {};
    const baseTemplateId = metadata.baseTemplateId;
    let baseContent = '';

    if (baseTemplateId) {
      const template = await fetchJson<{ source?: string }>(
        `${this.simApiUrl}/templates/${baseTemplateId}/source`,
        { method: 'GET' },
        this.requestTimeoutMs
      );
      baseContent = template.source ?? '';
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
}
