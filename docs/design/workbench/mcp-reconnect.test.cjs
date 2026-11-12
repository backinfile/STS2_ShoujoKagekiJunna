'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync(require.resolve('./app.js'),'utf8');
const start=source.indexOf('  async function syncIfIdle('),end=source.indexOf("  document.addEventListener('visibilitychange'",start);
function fixture(overrides={}){
  const calls={pull:0,flush:0};
  const context=vm.createContext({document:{visibilityState:'visible',activeElement:{matches:()=>true},querySelector:()=>null},navigator:{onLine:true},mcpConnecting:false,pending:null,mcpStatus:{ready:false,busy:false,conflict:null,pending:0},mcp:{async pull(){calls.pull++;},async flush(){calls.flush++;}},...overrides});
  vm.runInContext(source.slice(start,end),context);return{context,calls};
}
test('a disconnected phone reconnects while the search field or dialog remains focused',async()=>{
  for(const dialog of [null,{}]){const f=fixture();f.context.document.querySelector=()=>dialog;await f.context.syncIfIdle();assert.equal(f.calls.pull,1);}
});
test('connected field editing still postpones background refreshes',async()=>{
  const f=fixture();f.context.mcpStatus.ready=true;await f.context.syncIfIdle();assert.equal(f.calls.pull,0);
});
test('reconnection preserves local pending edits and retries queued drafts safely',async()=>{
  const f=fixture();f.context.pending={};await f.context.syncIfIdle();assert.equal(f.calls.pull,0);
  f.context.pending=null;f.context.mcpStatus.pending=1;await f.context.syncIfIdle();assert.equal(f.calls.flush,1);assert.equal(f.calls.pull,0);
});
test('offline, saving, conflicting and initial-connection states do not start another retry',async()=>{
  for(const override of [{navigator:{onLine:false}},{mcpConnecting:true},{mcpStatus:{busy:true}},{mcpStatus:{conflict:{code:'VERSION_CONFLICT'}}}]){const f=fixture(override);await f.context.syncIfIdle();assert.equal(f.calls.pull,0);assert.equal(f.calls.flush,0);}
});
