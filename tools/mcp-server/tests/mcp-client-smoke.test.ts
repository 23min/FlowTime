import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { CallToolResultSchema } from '@modelcontextprotocol/sdk/types.js';

const simApiUrl = process.env.MCP_TEST_SIM_API_URL;
const engineApiUrl = process.env.MCP_TEST_ENGINE_API_URL;

if (!simApiUrl || !engineApiUrl) {
  test('mcp client smoke test skipped (missing MCP_TEST_SIM_API_URL or MCP_TEST_ENGINE_API_URL)', { skip: true }, () => {
    assert.ok(true);
  });
} else {
  test('mcp client can list tools and call list_templates', async () => {
    const here = path.dirname(fileURLToPath(import.meta.url));
    const serverCwd = path.resolve(here, '..');

    const transport = new StdioClientTransport({
      command: 'node',
      args: ['--import', 'tsx', 'src/index.ts'],
      cwd: serverCwd,
      env: {
        FLOWTIME_SIM_API_URL: simApiUrl,
        FLOWTIME_API_URL: engineApiUrl,
        MCP_MAX_BINS: process.env.MCP_TEST_MAX_BINS ?? '1000'
      },
      stderr: 'pipe'
    });

    const client = new Client({ name: 'flowtime-mcp-smoke', version: '0.1.0' });

    try {
      await client.connect(transport);
      const tools = await client.listTools();
      const toolNames = tools.tools.map((tool) => tool.name);
      assert.ok(toolNames.includes('list_templates'));

      const result = await client.request(
        {
          method: 'tools/call',
          params: {
            name: 'list_templates',
            arguments: {}
          }
        },
        CallToolResultSchema
      );

      const structured = result.structuredContent as { templates?: unknown[] } | undefined;
      if (!structured) {
        throw new Error('Missing structured payload.');
      }
      assert.ok(Array.isArray(structured.templates));
    } finally {
      await client.close();
    }
  }, { timeout: 20000 });
}
