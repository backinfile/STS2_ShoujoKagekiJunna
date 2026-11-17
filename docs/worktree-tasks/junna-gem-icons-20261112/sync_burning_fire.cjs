'use strict';
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const [repository,clientPath]=process.argv.slice(2);
const {callTool}=require(clientPath);
const output=path.join(repository,'docs/worktree-tasks/junna-gem-icons-20261112/art-v5');
const assetId='relic-burning-gem-native-v5-20261112';
async function main(){
  const changes=[];
  for(const collection of ['relics','fitPool']){
    const uid=collection==='relics'?'relic_junna_burning_gem':'fit_relic_junna_burning_gem';
    const entry=await callTool('workbench_get_entity',{collection,uid});
    const entity=entry.entity;
    if(entity.art!=='relic-burning-gem-movie-fire-v4-20261112')throw Error('Concurrent art change: '+uid);
    const updates={art:assetId},expectedFields={art:entity.art};
    if(collection==='fitPool'){
      updates.notes='初始遗物绿色宝石的升级版，名称取自“燃える宝石のようなキラめき”。物件依据《Memory of Revue》书内119页纯那脇差柄端的不规则凸起宝石，去掉包托、底座和刀柄。用户否定发光特效画法后，整张图按尖塔本体遗物画风从零重画：火焰直接参照燃烧之血与卡戎之灰，采用红橙色火舌、少量黄芯和粗暗轮廓；矿石参照硫磺与贤者之石，采用有限的大色块、哑光明暗与轻微干笔笔触。宝石为主，火焰集中在上缘后方。'+entity.text+'当同时获得或者失去多张时，按张数计算回复值。内置image_gen仅输入原版图标和原作形状参考生成，经本地Artifact Cleaner原尺寸清理，导出128像素透明PNG及独立悬停轮廓。';
      expectedFields.notes=entity.notes;
    }
    changes.push({collection,uid,expectedVersion:entry.version,expectedFields,updates});
  }
  const bytes=fs.readFileSync(path.join(repository,'ShoujoKagekiJunna/images/relics/burning_gem.png'));
  const request={clientId:'codex-junna-gem-icons-20261112',requestId:crypto.randomUUID(),label:'按尖塔原版遗物画风从零重画燃烧宝石',changes,assets:{[assetId]:'data:image/png;base64,'+bytes.toString('base64')}};
  fs.writeFileSync(path.join(output,'workbench-draft.json'),JSON.stringify({...request,assets:{[assetId]:'<PNG asset data>'}},null,2)+'\n');
  const result=await callTool('workbench_commit',request);
  fs.writeFileSync(path.join(output,'workbench-receipt.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({saved:result.saved,revision:result.revision,requestId:request.requestId}));
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
