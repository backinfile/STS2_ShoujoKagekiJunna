'use strict';
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const [repository,clientPath]=process.argv.slice(2);
const {callTool}=require(clientPath);
const output=path.join(repository,'docs/worktree-tasks/junna-gem-icons-20261112/art-v4');
const assetId='relic-burning-gem-movie-fire-v4-20261112';
async function main(){
  const changes=[];
  for(const collection of ['relics','fitPool']){
    const uid=collection==='relics'?'relic_junna_burning_gem':'fit_relic_junna_burning_gem';
    const entry=await callTool('workbench_get_entity',{collection,uid});
    const entity=entry.entity;
    if(entity.art!=='relic-burning-gem-from-zero-v3-20261112')throw Error('Concurrent art change: '+uid);
    const updates={art:assetId},expectedFields={art:entity.art};
    if(collection==='fitPool'){
      updates.notes='初始遗物绿色宝石的升级版，名称取自“燃える宝石のようなキラめき”。图标实体依据《Memory of Revue》书内119页纯那脇差柄端的不规则凸起宝石，沿用从零绘制版的绿色切面与不规则造型，去掉包托、底座和刀柄。按用户要求加入剧场版狩猎的Revue金黄色燃烧效果：片段05:45的金黄色光焰与白亮火芯、06:09纯那短刃柄端的白亮光焰。火焰沿后方和边缘上卷，绿色主体清晰，外围留透明边距；图标及悬停轮廓同步更新。'+entity.text+'当同时获得或者失去多张时，按张数计算回复值。使用内置image_gen编辑，经本地Artifact Cleaner在原尺寸清理，导出128像素透明PNG。';
      expectedFields.notes=entity.notes;
    }
    changes.push({collection,uid,expectedVersion:entry.version,expectedFields,updates});
  }
  const bytes=fs.readFileSync(path.join(repository,'ShoujoKagekiJunna/images/relics/burning_gem.png'));
  const request={clientId:'codex-junna-gem-icons-20261112',requestId:crypto.randomUUID(),label:'为燃烧宝石添加剧场版金黄色光焰',changes,assets:{[assetId]:'data:image/png;base64,'+bytes.toString('base64')}};
  fs.writeFileSync(path.join(output,'workbench-draft.json'),JSON.stringify({...request,assets:{[assetId]:'<PNG asset data>'}},null,2)+'\n');
  const result=await callTool('workbench_commit',request);
  fs.writeFileSync(path.join(output,'workbench-receipt.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({saved:result.saved,revision:result.revision,requestId:request.requestId}));
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
