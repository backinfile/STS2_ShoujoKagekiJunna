'use strict';
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const [repository,clientPath]=process.argv.slice(2);
const {callTool}=require(clientPath);
const task=path.join(repository,'docs/worktree-tasks/junna-gem-icons-20261112');
const output=path.join(task,'rollback-original');
async function main(){
  const original=JSON.parse(fs.readFileSync(path.join(task,'workbench-draft.json'),'utf8'));
  const changes=[];
  for(const previous of original.changes){
    const {collection,uid}=previous;
    const entry=await callTool('workbench_get_entity',{collection,uid});
    const entity=entry.entity;
    const current=uid.includes('burning')?'relic-burning-gem-native-v5-20261112':'relic-green-gem-from-zero-v3-20261112';
    if(entity.art!==current)throw Error('Concurrent art change: '+uid);
    const updates={art:previous.expectedFields.art},expectedFields={art:entity.art};
    if(collection==='fitPool'){
      updates.notes=previous.expectedFields.notes.replace(/每当牌组中增加或者失去卡牌时，回复\d+点生命。/,entity.text);
      expectedFields.notes=entity.notes;
    }
    changes.push({collection,uid,expectedVersion:entry.version,expectedFields,updates});
  }
  const request={clientId:'codex-junna-gem-icons-20261112',requestId:crypto.randomUUID(),label:'按用户要求恢复对话开始前的原版遗物配图',changes};
  fs.writeFileSync(path.join(output,'workbench-draft.json'),JSON.stringify(request,null,2)+'\n');
  const result=await callTool('workbench_commit',request);
  fs.writeFileSync(path.join(output,'workbench-receipt.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({saved:result.saved,revision:result.revision,requestId:request.requestId}));
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
