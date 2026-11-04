'use strict';
// Compatibility entry point: legacy launchers now start the same MCP daemon.
const {ensureConfig}=require('./mcp-config.cjs');
if(require.main===module)require('./mcp-daemon.cjs').start();
module.exports={ensureConfig};
