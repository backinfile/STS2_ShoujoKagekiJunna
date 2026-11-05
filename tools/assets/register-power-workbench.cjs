// Register Power designs through the same versioned MCP service used by the workbench.
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const assert=require('node:assert/strict');
const {callTool}=require('../../docs/design/workbench/mcp-client.cjs');
const root=path.resolve(__dirname,'../..');
const read=file=>JSON.parse(fs.readFileSync(path.join(root,file),'utf8'));
const unpack=result=>{if(result.isError)throw Error(result.content[0].text);return result.structuredContent||(result.content?JSON.parse(result.content[0].text):result);};
const catalogIds=[...fs.readFileSync(path.join(root,'src/Models/CatalogPowers.Generated.cs'),'utf8').matchAll(/CatalogPower\("([^"]+)"\)/g)].map(m=>m[1]);
const nativeManifest='docs/design/assets/powers/2026-11-05/native-v3/manifest.json';
const manifestPath=fs.existsSync(path.join(root,nativeManifest))?nativeManifest:'docs/design/assets/powers/2026-11-05/manifest.json';
const manifest=read(manifestPath);
const cleanupPath=path.posix.join(path.posix.dirname(manifestPath),'cleanup.json');
const cleanup=read(cleanupPath);
assert.equal(catalogIds.length,26);assert.equal(cleanup.assets.length,31);
const temporary=new Set(['JK64','JG31']);
const once=new Set(['JA04','JH19','JK80']);
function effect(card,upgraded=false){
  let text=String(upgraded?card.upgradedText:card.text).replace(/^知识。/,'').replace(/消耗。/g,'').replace(/打出打出/g,'打出');
  if(card.id==='JH19')text=text.slice(text.indexOf('下一张'));
  if(card.id==='JH24')text=text.slice(text.indexOf('每回合'));
  if(card.id==='JK80')text='下次打出知识牌后，获得'+text.match(/再获得(\d+)点格挡/)[1]+'点格挡。';
  return text;
}
(async()=>{
  const state=unpack(await callTool('workbench_get_state',{includeAssets:false,includeHistory:false}));
  const cards=state.bundle.data.cards.filter(c=>!c.trashedAt&&!c.reservedAt),cardById=id=>{const card=cards.find(c=>c.id===id);assert(card,'Missing active source card: '+id);return card;};
  const make=(key,id,cardIds,extra={})=>{
    const spec=manifest.icons.find(e=>e.key===key);assert(spec,key);
    const sources=cardIds.map(cardById);
    return {uid:'power_'+id.toLowerCase(),id,name:spec.name,origin:'junna',type:'增益',system:sources[0].system,
      duration:temporary.has(key)?'本回合':once.has(key)?'触发后移除':'本场战斗',
      text:effect(sources[0]),upgradedText:effect(sources[0],true),cardIds,
      iconDesign:spec.fit,source:[...new Set(sources.map(c=>c.naming?.work).filter(Boolean))].join('；'),
      sourceUrl:sources[0].naming?.url||'',art:spec.assetId||'power-icon-'+key.toLowerCase().replace(/_/g,'-')+'-v'+(spec.version||1)+'-20261105',
      iconPath:spec.icon,originalIconPath:spec.original,vectorPath:spec.svg,iconStyle:manifest.style,nativeIconPath:spec.nativeSource||null,iconSize:spec.size||manifest.size,...extra};
  };
  const powers=catalogIds.map(cid=>make(cid,'JUNNA_'+cid+'_POWER',[cid],{implementation:'src/Models/CatalogPower.cs',model:'JunnaPower'+cid}));
  powers.push(make('JG23','JUNNA_ENDURANCE_POWER',['JG23'],{implementation:'src/Models/JunnaEndurancePower.cs',model:'JunnaEndurancePower'}));
  powers.push(make('ENERGY_RESERVE','JUNNA_ENERGY_RESERVE',['JX07','JH20','JH26','JA80'],{system:'energy',duration:'下一回合',text:'下回合保留至多X点剩余能量。',upgradedText:'下回合保留至多X点剩余能量。',implementation:'src/Combat/EnergyReserve.cs',model:'EnergyReserve'}));
  powers.push(make('ENERGY_NEXT_TURN','ENERGY_NEXT_TURN_POWER',['JA16','JA17'],{origin:'base',system:'energy',duration:'下一回合',text:'下回合开始时，获得X点能量。',upgradedText:'下回合开始时，获得X点能量。',implementation:'src/Models/Cards.cs',model:'EnergyNextTurnPower'}));
  powers.push(make('VULNERABLE','VULNERABLE_POWER',['JF10','JA58'],{origin:'base',type:'减益',system:'utility',duration:'按层数',text:'受到的攻击伤害增加50%。',upgradedText:'受到的攻击伤害增加50%。',implementation:'src/Combat/CardEffects.cs',model:'VulnerablePower'}));
  powers.push(make('WEAK','WEAK_POWER',['JF10','JA58'],{origin:'base',type:'减益',system:'utility',duration:'按层数',text:'攻击造成的伤害降低25%。',upgradedText:'攻击造成的伤害降低25%。',implementation:'src/Combat/CardEffects.cs',model:'WeakPower'}));
  assert.equal(powers.length,31);assert.equal(new Set(powers.map(p=>p.id)).size,31);
  const assets=Object.fromEntries(powers.map(power=>[power.art,'data:image/png;base64,'+fs.readFileSync(path.join(root,power.iconPath)).toString('base64')]));
  const current=unpack(await callTool('workbench_get_entity',{collection:'root',uid:'meta'}));
  const before=current.entity,after=structuredClone(before),existing=new Map((before.powers||[]).map(p=>[p.id,p]));
  if(manifest.styleReference&&before.powers?.length){
    after.powers=before.powers.map(old=>{const design=powers.find(p=>p.id===old.id);if(!design)return old;const next={...old};for(const key of ['art','iconDesign','iconPath','originalIconPath','vectorPath','iconStyle','nativeIconPath','iconSize']){if(design[key]===null)delete next[key];else next[key]=design[key];}return next;});
  }else after.powers=powers.map(power=>{const old=existing.get(power.id);return {...power,...old,...(old?.art?.startsWith('power-icon-')&&old.art!==power.art?{art:power.art,iconDesign:power.iconDesign}:{} )};});
  for(const old of before.powers||[])if(!after.powers.some(p=>p.id===old.id))after.powers.push(old);
  const entries=await Promise.all(after.powers.map(async design=>{
    const currentPower=unpack(await callTool('workbench_get_entity',{collection:'powers',uid:design.uid}));
    const next=currentPower.entity?{...currentPower.entity}:design;
    if(currentPower.entity)for(const key of ['art','iconDesign','iconPath','originalIconPath','vectorPath','iconStyle','nativeIconPath','iconSize']){if(Object.hasOwn(design,key))next[key]=design[key];else delete next[key];}
    return {collection:'powers',uid:design.uid,expectedVersion:currentPower.version,before:currentPower.entity,after:next};
  }));
  const changes=entries.filter(entry=>JSON.stringify(entry.before)!==JSON.stringify(entry.after));
  const result=unpack(await callTool('workbench_commit',{requestId:crypto.randomUUID(),clientId:'codex-power-workbench',label:manifest.styleReference?'按本体Power风格修正31枚图标':'新增Power目录与31枚契合图标',kind:'设计',changes,assets}));
  const audit={revision:result.revision,powers:powers.length,custom:powers.filter(p=>p.origin==='junna').length,builtin:powers.filter(p=>p.origin==='base').length,
    sourceCards:[...new Set(powers.flatMap(p=>p.cardIds))],sourceScope:{activeCards:cards.length,rewardCards:cards.filter(c=>c.pool).length,includesKnowledgePartners:true,includesTemporaryEffects:true},
    powerIds:powers.map(p=>p.id),cleanup:cleanupPath};
  fs.writeFileSync(path.join(root,'artifacts/power-workbench/coverage.json'),JSON.stringify(audit,null,2)+'\n');
  console.log(JSON.stringify({revision:result.revision,powers:audit.powers,custom:audit.custom,builtin:audit.builtin,sourceCards:audit.sourceCards.length}));
})().catch(error=>{console.error(error);process.exitCode=1;});
