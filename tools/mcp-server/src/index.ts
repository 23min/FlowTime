import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { loadConfig } from './config.js';
import { createHandlers } from './handlers.js';
import { registerTools } from './tools.js';

const main = async (): Promise<void> => {
  const config = loadConfig();
  const server = new McpServer({
    name: 'flowtime-mcp-server',
    version: '0.1.0'
  });

  registerTools(server, createHandlers(config));

  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error('FlowTime MCP server running on stdio');
};

main().catch((error) => {
  console.error('FlowTime MCP server failed to start:', error);
  process.exit(1);
});
