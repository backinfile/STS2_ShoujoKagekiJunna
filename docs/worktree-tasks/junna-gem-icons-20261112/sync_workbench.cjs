'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const [repository, clientPath] = process.argv.slice(2);
const {callTool} = require(clientPath);
const taskPath = path.join(repository,'docs/worktree-tasks/junna-gem-icons-20261112');
async function main() {
  const assets={},changes=[];
  for (const [key,uid,oldArt,asset] of [
    ['green_gem','relic_junna_green_gem','relic-green-gem-vector-v2-20261105','relic-green-gem-bare-20261112'],
    ['burning_gem','relic_junna_burning_gem','relic-burning-gem-vector-v2-20261105','relic-burning-gem-bare-20261112']]) {
    assets[asset]='data:image/png;base64,'+fs.readFileSync(path.join(repository,'ShoujoKagekiJunna/images/relics',key+'.png')).toString('base64');
    for (const collection of ['relics','fitPool']) {
      const entry=await callTool('workbench_get_entity',{collection,uid:collection==='fitPool'?'fit_'+uid:uid});
      const entity=entry.entity;
      if(entity.art!==oldArt) throw Error('Concurrent art change: '+entity.uid);
      const updates={art:asset},expectedFields={art:entity.art};
      if(collection==='fitPool') {
        updates.notes=(key==='green_gem'?'纯那弓端发光的绿色宝石。':'初始遗物绿色宝石的升级版，名称取自“燃える宝石のようなキラめき”。')+entity.text+'当同时获得或者失去多张时，按张数计算回复值。图标依据《少女☆歌劇 レヴュースタァライト 設定資料集》“翡翠弓”页的“宝石のUP”（公开预览第32张）及TV第2话17:07近景重绘。按用户要求仅保留完整宝石，去掉包托和底座。两版沿用同一切面布局，升级版增强内部白光、青绿色溢光及放射光芒。透明PNG由内置image_gen生成，经本地Artifact Cleaner按原尺寸处理后导出128像素。Memory of Revue读者记录提及祖母绿，材质原文尚待直接核对。';
        expectedFields.notes=entity.notes;
      }
      changes.push({collection,uid:entity.uid,expectedVersion:entry.version,expectedFields,updates});
    }
  }
  const request={clientId:'codex-junna-gem-icons-20261112',requestId:crypto.randomUUID(),label:'按设定图重绘纯那裸宝石初始及升级遗物图标',changes,assets};
  const draft={...request,assets:Object.fromEntries(Object.keys(assets).map(id=>[id,'<PNG asset data>']))};
  fs.writeFileSync(path.join(taskPath,'workbench-draft.json'),JSON.stringify(draft,null,2)+'\n');
  const result=await callTool('workbench_commit',request);
  fs.writeFileSync(path.join(taskPath,'workbench-receipt.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({ok:result.ok,revision:result.revision,requestId:request.requestId}));
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
