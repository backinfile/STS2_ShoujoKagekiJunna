'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const base = __dirname;
const root = path.resolve(base, '../../../../..');
const {callTool} = require(path.join(root, 'docs/design/workbench/mcp-client.cjs'));
const read = file => JSON.parse(fs.readFileSync(path.join(base, file), 'utf8'));
const rel = file => path.relative(root, file).replaceAll('\\', '/');
const style = '参照本体 Power 重绘：紧凑单主体、黑色粗轮廓、饱和主色与大块明暗，透明底；以32像素辨识度检查。';
async function main() {
  const plan = read('design-plan.json');
  const snapshot = read('source-snapshot.json');
  const cleanup = read('cleanup.json');
  assert.equal(plan.icons.length, 36);
  assert.equal(cleanup.assets.length, 36);
  const changes = [], assets = {}, manifest = [], baselines = [];
  for (let i = 0; i < plan.icons.length; i++) {
    const spec = plan.icons[i], original = snapshot[i].entity;
    assert.equal(spec.name, original.name);
    const current = await callTool('workbench_get_entity', {collection:'powers', uid:original.uid});
    assert(current.entity, original.uid);
    assert.equal(current.entity.art, original.art, '图标在绘制期间已被修改，请保留草稿并合并：'+original.uid);
    baselines.push(current.entity);
    const icon = path.join(base, 'icons', spec.key+'.png');
    const assetId = 'power-icon-'+spec.key.toLowerCase().replaceAll('_','-')+'-redraw-20261108';
    const updates = {art:assetId, iconDesign:spec.fit, iconPath:rel(icon), originalIconPath:rel(path.join(base,'generated',spec.key+'.png')), iconStyle:style, iconSize:[256,256], iconCleanup:{tool:cleanup.tool, strength:.25, interiorBlend:.25, record:rel(path.join(base,'cleanup.json'))}};
    const expectedFields = {}, expectedAbsent = [];
    const unset = ['vectorPath','nativeIconPath'].filter(k=>Object.hasOwn(current.entity,k));
    for (const k of [...Object.keys(updates), ...unset]) {
      if (Object.hasOwn(current.entity,k)) expectedFields[k] = current.entity[k];
      else expectedAbsent.push(k);
    }
    changes.push({collection:'powers', uid:original.uid, expectedVersion:current.version, expectedFields, expectedAbsent, updates, ...(unset.length?{unset}:{})});
    assets[assetId]='data:image/png;base64,'+fs.readFileSync(icon).toString('base64');
    manifest.push({...spec, uid:original.uid, id:original.id, assetId, file:rel(icon), original:updates.originalIconPath});
  }
  const request={requestId:crypto.randomUUID(),clientId:'codex-power-redraw-20261108',label:'按本体图标语言重绘全部36项Power并经本地Artifact Cleaner处理',kind:'设计',changes,assets};
  fs.writeFileSync(path.join(base,'commit-request.json'),JSON.stringify(request,null,2)+'\n');
  const result=await callTool('workbench_commit',request);
  fs.writeFileSync(path.join(base,'commit-receipt.json'),JSON.stringify(result,null,2)+'\n');
  const checks=[];
  for(const entry of manifest){
    const current=await callTool('workbench_get_entity',{collection:'powers',uid:entry.uid});
    assert.equal(current.entity.art,entry.assetId);
    const original=baselines.find(p=>p.uid===entry.uid);
    for(const k of ['name','text','upgradedText','cardIds','localization'])assert.deepEqual(current.entity[k],original[k],entry.uid+':'+k);
    checks.push({uid:entry.uid,assetId:entry.assetId,version:current.version});
  }
  fs.writeFileSync(path.join(base,'manifest.json'),JSON.stringify({revision:result.revision,style,icons:manifest,checks},null,2)+'\n');
  console.log(JSON.stringify({revision:result.revision,updated:checks.length,requestId:request.requestId}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
