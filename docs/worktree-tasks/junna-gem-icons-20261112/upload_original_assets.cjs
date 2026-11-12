'use strict';
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const [repository,clientPath]=process.argv.slice(2);
const {callTool}=require(clientPath);
const task=path.join(repository,'docs/worktree-tasks/junna-gem-icons-20261112');
const output=path.join(task,'rollback-original/workbench-upload');
async function main(){
  fs.mkdirSync(output,{recursive:true});
  const changes=[],assets={},sources=[];
  for(const key of ['green','burning']){
    const oldArt=`relic-${key}-gem-vector-v2-20261105`;
    const art=`relic-${key}-gem-project-original-20261112`;
    const file=path.join(task,`rollback-original/${key}_gem.png`);
    const bytes=fs.readFileSync(file);
    assets[art]='data:image/png;base64,'+bytes.toString('base64');
    sources.push({art,path:path.relative(repository,file).replaceAll('\\','/'),sha256:crypto.createHash('sha256').update(bytes).digest('hex')});
    for(const collection of ['relics','fitPool']){
      const uid=(collection==='fitPool'?'fit_':'')+`relic_junna_${key}_gem`;
      const entry=await callTool('workbench_get_entity',{collection,uid});
      const entity=entry.entity;
      if(entity.art!==oldArt)throw Error('Concurrent art change: '+uid);
      const updates={art},expectedFields={art:entity.art};
      if(collection==='fitPool'){
        const phrase=key==='green'?'两版沿用相同的多面切割主体与羽叶状金色包托':'两版保留完全相同的宝石切面轮廓与羽叶状金色包托';
        const replacement=key==='green'?'两版沿用相同的多面切割主体，当前图标仅保留宝石':'两版保留相同的宝石切面轮廓，当前图标仅保留宝石';
        if(!entity.notes.includes(phrase))throw Error('Concurrent notes change: '+uid);
        updates.notes=entity.notes.replace(phrase,replacement);
        expectedFields.notes=entity.notes;
      }
      changes.push({collection,uid,expectedVersion:entry.version,expectedFields,updates});
    }
  }
  const request={clientId:'codex-junna-gem-icons-20261112',requestId:crypto.randomUUID(),label:'上传项目原有裸宝石SVG导出图，修正旧素材ID中的包托版',changes,assets};
  fs.writeFileSync(path.join(output,'draft.json'),JSON.stringify({...request,assets:Object.fromEntries(Object.keys(assets).map(k=>[k,'<PNG asset data>']))},null,2)+'\n');
  fs.writeFileSync(path.join(output,'sources.json'),JSON.stringify(sources,null,2)+'\n');
  const result=await callTool('workbench_commit',request);
  fs.writeFileSync(path.join(output,'receipt.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({saved:result.saved,revision:result.revision,requestId:request.requestId}));
  if(result.saved!==true)throw Error('Workbench upload did not save');
  const verification=[];
  for(const {collection,uid,updates} of changes){
    const current=await callTool('workbench_get_entity',{collection,uid});
    if(current.entity.art!==updates.art)throw Error('Post-save art mismatch: '+uid);
    verification.push({collection,uid,art:current.entity.art,text:current.entity.text,version:current.version});
  }
  const urls=await callTool('workbench_get_assets',{ids:sources.map(s=>s.art),asURLs:true});
  for(const source of sources){
    const url='https://junna.17181718.xyz'+urls.assets[source.art];
    const response=await fetch(url);
    if(!response.ok)throw Error('Public asset request failed: '+response.status);
    const hash=crypto.createHash('sha256').update(Buffer.from(await response.arrayBuffer())).digest('hex');
    if(hash!==source.sha256)throw Error('Public asset bytes differ: '+source.art);
    source.url=url;source.publicMatchesLocal=true;
  }
  fs.writeFileSync(path.join(output,'verification.json'),JSON.stringify({revision:result.revision,entries:verification,assets:sources},null,2)+'\n');
  console.log('Verified four entries and both public image hashes.');
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
