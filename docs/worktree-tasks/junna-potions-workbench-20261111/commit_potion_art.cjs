'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const clientPath=process.argv[2];if(!clientPath)throw Error('Pass the configured workbench MCP client module.');
const {callTool}=require(path.resolve(clientPath));
const readJSON=file=>JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
async function main(){
  const version=process.argv[3]||'v1';if(!['v1','v2','v3','v4','v5','v6','v7','v8','v9','v10'].includes(version))throw Error('Unsupported art version');
  const suffix=version==='v1'?'':'-'+version;
  const base='docs/design/assets/potions/2026-11-11'+(version==='v1'?'':'/'+version);
  const task='docs/worktree-tasks/junna-potions-workbench-20261111';
  const request=readJSON(task+'/workbench-art'+suffix+'-updates.json'),manifest=readJSON(base+'/manifest.json'),cleanup=readJSON(base+'/cleanup.json');
  const assets={};
  for(const item of manifest.icons){
    const bytes=fs.readFileSync(item.file),record=cleanup.assets.find(row=>row.key===item.key);
    if(!record||crypto.createHash('sha256').update(bytes).digest('hex')!==record.sha256)throw Error('Icon checksum mismatch: '+item.key);
    assets[item.assetId]='data:image/png;base64,'+bytes.toString('base64');
  }
  const result=await callTool('workbench_commit',{...request,assets});
  fs.writeFileSync(task+'/workbench-art'+suffix+'-receipt.json',JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({saved:result.saved,revision:result.revision,requestId:request.requestId,assetIds:result.assetIds,versions:result.versions}));
}
main().catch(error=>{console.error(JSON.stringify({code:error.code,message:error.message,conflicts:error.conflicts}));process.exitCode=1;});
