'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const [repository, clientPath] = process.argv.slice(2);
const {callTool} = require(clientPath);
const taskPath = path.join(repository,'docs/worktree-tasks/junna-gem-icons-20261112/art-v3');
async function main() {
  const assets={},changes=[];
  for (const [key,uid,oldArt,asset] of [
    ['green_gem','relic_junna_green_gem','relic-green-gem-bare-20261112','relic-green-gem-from-zero-v3-20261112'],
    ['burning_gem','relic_junna_burning_gem','relic-burning-gem-bare-20261112','relic-burning-gem-from-zero-v3-20261112']]) {
    assets[asset]='data:image/png;base64,'+fs.readFileSync(path.join(repository,'ShoujoKagekiJunna/images/relics',key+'.png')).toString('base64');
    for (const collection of ['relics','fitPool']) {
      const entry=await callTool('workbench_get_entity',{collection,uid:collection==='fitPool'?'fit_'+uid:uid});
      const entity=entry.entity;
      if(entity.art!==oldArt) throw Error('Concurrent art change: '+entity.uid);
      const updates={art:asset},expectedFields={art:entity.art};
      if(collection==='fitPool') {
        const design=key==='green_gem'
          ? '纯那弓端完整的绿色宝石。造型依据《少女☆歌劇 レヴュースタァライト 設定資料集》“翡翠弓”页的“宝石のUP”（公开预览第32张），配色参考剧场版角色彩图及TV第2话近景，采用圆润完整外轮廓与放射三角切面。'
          : '初始遗物绿色宝石的升级版，名称取自“燃える宝石のようなキラめき”。图标造型依据《Memory of Revue》书内119页（公开预览第122张）纯那脇差柄端“宝石のUP”，突出不规则、非对称凸起与断面，以集中青白亮光表现燃烧般的光芒；与TV设定中奈奈原刀柄标注的托帕石分开记录。';
        updates.notes=design+entity.text+'当同时获得或者失去多张时，按张数计算回复值。按用户要求只保留宝石，去掉包托、底座与刀柄。两图均由内置image_gen仅使用原作参考从零独立生成，采用各自的造型与悬停轮廓，经本地Artifact Cleaner原尺寸处理后导出128像素透明PNG。';
        expectedFields.notes=entity.notes;
      }
      changes.push({collection,uid:entity.uid,expectedVersion:entry.version,expectedFields,updates});
    }
  }
  const request={clientId:'codex-junna-gem-icons-20261112',requestId:crypto.randomUUID(),label:'从零独立重绘弓端完整宝石与纯那短刃不规则宝石',changes,assets};
  const draft={...request,assets:Object.fromEntries(Object.keys(assets).map(id=>[id,'<PNG asset data>']))};
  fs.writeFileSync(path.join(taskPath,'workbench-draft.json'),JSON.stringify(draft,null,2)+'\n');
  const result=await callTool('workbench_commit',request);
  fs.writeFileSync(path.join(taskPath,'workbench-receipt.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({ok:result.ok,revision:result.revision,requestId:request.requestId}));
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
