'use strict';
const path = require('node:path');

const coreRoot = path.resolve(process.env.SHOUJO_CORE_ROOT || path.join(__dirname, '../../../STS2_ShoujoKagekiAijoCore'));
const { callTool: callCoreTool } = require(path.join(coreRoot, 'tools/workbench/mcp-client.cjs'));

function callTool(name, args = {}) {
  return callCoreTool(name, { ...args, modId: 'junna' });
}

module.exports = { callTool };
