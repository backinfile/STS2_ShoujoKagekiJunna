'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const {callTool}=require(path.resolve(__dirname,'../../design/workbench/mcp-client.cjs'));
const plan=JSON.parse(fs.readFileSync(path.join(__dirname,'prepared-plan.json'),'utf8'));
const target=new Map(plan.map(x=>[x.id,x]));
const companionIds=new Set(['JK15B','JK84B','JK200B']);
const groupRefs=new Set(['GROUP_JK232','GROUP_JK233','GROUP_JK234','GROUP_JK236','GROUP_JK237']);
const interview='https://hon-hikidashi.jp/event/42802/';
const clone=x=>JSON.parse(JSON.stringify(x));
const same=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
const replace=(value,pairs)=>{
 if(typeof value==='string'){for(const [a,b]of pairs)value=value.split(a).join(b);return value;}
 if(Array.isArray(value))return value.map(x=>replace(x,pairs));
 if(value&&typeof value==='object')return Object.fromEntries(Object.entries(value).map(([k,v])=>[k,replace(v,pairs)]));
 return value;
};
const pairs=[['旁征博引','文字，文字，文字'],['回到那一页','再演'],['永不折断的笔','全力释放'],['吹吧，狂风','呼喊'],['誊写','铭刻'],['站起来','伤痕']];
const readFile=name=>JSON.parse(fs.readFileSync(path.join(__dirname,name),'utf8'));
const save=(name,value)=>fs.writeFileSync(path.join(__dirname,name),JSON.stringify(value,null,2)+'\n');
function artInfo(row,oldCard,state){
 const key=row.asset||row.reuse||oldCard.art;
 let proof=row.artProof||'',source=row.sourceFile||'';
 const url=row.artUrl||row.artPage||oldCard.naming?.artSourceUrl||'';
 if(row.sourceFile){
  const [l,t,r,b]=row.crop;
  proof=`${source}；原尺寸${row.sourceSize.join('×')}；裁切x=${l},y=${t},w=${r-l},h=${b-t}；成品${row.outputFile}；SHA256 ${row.sha256}`;
 }else if(row.reuse){
  const from=state.data.cards.find(c=>c.art===row.reuse&&target.has(c.id)&&c.id!==row.id);
  if(from)proof+=` 原素材出处：${from.naming?.artProof||from.naming?.proof||from.naming?.artSourceUrl||row.artUrl}`;
 }
 return {key,url,proof,source,work:row.artWork||oldCard.naming?.artWork||''};
}
async function prepare(){
 const cached=process.argv.includes('--reuse-read')?readFile('commit-before-entities.json'):null;
 const cacheData={};
 if(cached)for(const r of cached)(cacheData[r.collection]||=[]).push(r.entity);
 const raw=cached?{revision:readFile('commit-review.json').revision,bundle:{data:cacheData},versions:{}}:await callTool('workbench_get_state',{includeAssets:false,includeHistory:false,assetURLs:true});
 const state={revision:raw.revision,data:raw.bundle.data,versions:raw.versions};
 const selected=[];
 for(const [collection,list]of Object.entries(state.data)){
  if(!Array.isArray(list))continue;
  for(const e of list){
   if(e.trashedAt)continue;
   const relevant=collection==='cards'&&(target.has(e.id)||companionIds.has(e.id)||e.id==='JK235')
    ||collection==='fitPool'&&(e.cardIds||[]).some(id=>target.has(id)||companionIds.has(id))
    ||collection==='partnerGroups'&&(target.has(e.mainCardId)||(e.grantedCardIds||[]).some(id=>target.has(id))||groupRefs.has(e.id))
    ||collection==='styles'&&['junna_style_art_review','junna_style_confirmed_names'].includes(e.uid);
   if(relevant)selected.push({collection,uid:e.uid});
  }
 }
 const records=cached||[];
 for(let i=0;!cached&&i<selected.length;i+=6){
  const chunk=await Promise.all(selected.slice(i,i+6).map(async s=>({...s,...await callTool('workbench_get_entity',s)})));
  records.push(...chunk);
 }
 const cards=new Map(records.filter(x=>x.collection==='cards').map(x=>[x.entity.id,x.entity]));
 const currentNames=new Map([...cards].map(([id,c])=>[id,target.get(id)?.name||c.name]));
 const byCard=new Map();
 for(const row of plan){
  const card=cards.get(row.id);if(!card)throw Error('Missing card '+row.id);
  byCard.set(row.id,{row,card,art:artInfo(row,card,state)});
 }
 const changes=[];
 for(const rec of records){
  const old=rec.entity;let next=clone(old);
  if(rec.collection==='cards'){
   if(target.has(old.id)){
    const {row,art}=byCard.get(old.id);
    next=replace(next,pairs);
    if(['JK206','JK206B'].includes(old.id))next=replace(next,[['查阅','读些什么']]);
    if(row.name)next.name=row.name;
    if(row.asset||row.reuse)next.art=art.key;
    if(row.focus)next.arrowFocus={x:row.focus[0],y:row.focus[1]};
    const previousFit=old.naming?.adaptation;
    next.naming={...next.naming,adaptation:row.fit};
    if(row.name){
     Object.assign(next.naming,{original:row.original,translation:row.name,chineseOriginal:'',work:row.work,url:row.url,
      kind:old.id.startsWith('JK206')?'文学原文连续原词的忠实自然译名':'原作连续原词的忠实自然译名'});
     if('proof'in next.naming)next.naming.proof=row.url;
    }
    if(old.id==='JK200')Object.assign(next.naming,{translation:next.name,kind:'文学场景中的呼喊行为名称'});
    if(old.id==='JK82'){
     next.naming.work='常用学习概念；TV特典第2话的角色解读讨论（配图）';next.naming.url=row.artPage;
     delete next.naming.artException;
    }
    if(old.id==='JK260')Object.assign(next.naming,{work:'常用知识概念；TV第11话纯那提供英语参考书（配图）',url:row.artPage});
    if(old.id!=='JK15'){
     Object.assign(next.naming,{artWork:art.work,artSourceUrl:art.url,artProof:art.proof,
      artProcessing:row.reuse?'复用已登记原作裁图，保留原素材出处。':'Pillow 12.1.1；原作截图矩形裁切，LANCZOS等比缩放至960×720，JPEG质量95；保留原始截图。'});
     if(next.naming.review)next.naming.review={...next.naming.review,artCheck:'2026-11-17对照纯那在用及备用图和Karen实际打包图，排除同一动作的相邻帧与换裁切。',composition:'复核普通卡框、基础与升级版；飞矢及循环攻击另按菱形遮罩调整聚焦。'};
    }
    next.design={...next.design,fit:row.fit};
    if(previousFit&&old.notes?.includes(previousFit))next.notes=next.notes.replace(replace(previousFit,pairs),row.fit);
    if(['JK206','JK206B'].includes(old.id)){
     next.design.groupTheme='《哈姆雷特》的读书问答：选择知识与运用字句';
     next.design.knowledgeContent='《哈姆雷特》第二幕第二场，波洛涅斯询问哈姆雷特在读什么，哈姆雷特以反复的“文字”作答。';
     next.naming.readingSourceUrl=interview;
     const description='《哈姆雷特》第二幕第二场，波洛涅斯问哈姆雷特读些什么，得到“文字，文字，文字”的回答。';
     next.extraHoverTips=(next.extraHoverTips||[]).filter(x=>x.id!=='junna_knowledge_background');
     next.extraHoverTips.push({id:'junna_knowledge_background',title:'知识背景',description});
    }
    if(old.id==='JK206B'){
     next.direction='从抽牌堆选出当前需要的知识。';
     next.notes=next.notes.replace('基础版消耗，升级移除消耗','基础及升级版均消耗，选择的知识牌数量为1／2');
     next.design.classification='知识组检索';
     next.design.rules=`按当前基础及升级牌面执行：${old.text}／${old.upgradedText}`;
     next.design.upgrade='选择的知识牌数量1→2，费用0与消耗保持。';
     next.design.roleReason='按需选出知识牌，支持知识运转。';
    }
    if(old.id==='JK84')next.design.knowledgeContent='舞台筹备与演出配合：在开演前安排后续行动，并与同伴互相支持。';
   }else{
    next=replace(next,pairs);
    if(old.id==='JK84B'){
     const fit='与相信我们共同取自《舞台プレパレイション》中鼓励演员放心发挥的一句歌词。相信我们表现后台筹备与支持，尽情大闹表现准备完成后主动出手、推进演出。';
     next.naming={...next.naming,adaptation:fit};next.design={...next.design,fit};
    }
   }
  }else if(rec.collection==='fitPool'){
   const match=(old.cardIds||[]).find(id=>target.has(id));
   if(match){
    const {row,card,art}=byCard.get(match);
    next=replace(next,pairs);
    if(['JK206','JK206B'].includes(match))next=replace(next,[['查阅','读些什么']]);
    Object.assign(next,{name:row.name||card.name,text:row.fit,art:art.key});
    if(row.name)Object.assign(next,{original:row.original,source:row.work,sourceUrl:row.url});
    if('chinese'in next)next.chinese=next.name;
    if(match==='JK82')Object.assign(next,{source:'常用学习概念；TV特典第2话的角色解读讨论（配图）',sourceUrl:row.artPage});
    if(match==='JK260')Object.assign(next,{source:'常用知识概念；TV第11话纯那提供英语参考书（配图）',sourceUrl:row.artPage});
    if(match==='U_CARD_008')next.source='名称来自《ディスカバリー！》，スタァライト九九組演唱、中村彼方作词；配图为TV第7话约08:22。';
    if(['JK206','JK206B'].includes(match))next.theme='《哈姆雷特》的读书问答';
    if(match==='JZ03')next.theme='物理箭的释放与抵达';
    if(match==='JK127')next.theme='排练记忆的保存';
    if(match==='JA96')next.theme='受伤后的继续应战';
    if(match!=='JK15'){
     next.notes=[art.work,art.url,art.proof].filter(Boolean).join('\n');
     if(['JK206','JK206B','JK200'].includes(match))next.notes+='\n角色阅读联系：'+interview;
     next.artSource={work:art.work,url:art.url,assetId:art.key,...(row.sourceFile?{localSource:row.sourceFile,crop:{x:row.crop[0],y:row.crop[1],width:row.crop[2]-row.crop[0],height:row.crop[3]-row.crop[1]},sha256:row.sha256}:{reuse:true})};
    }
   }else{
    next=replace(next,pairs);
    if((old.cardIds||[]).includes('JK84B')){
     next.text='与相信我们共同表现后台筹备完成后的放心发挥；本牌以主动攻击带动后续演出与飞矢推进。';
    }
   }
  }else if(rec.collection==='partnerGroups'){
   next=replace(next,pairs);
   if(old.id==='GROUP_JK200')next.title=[old.mainCardId,...old.grantedCardIds].map(id=>currentNames.get(id)||id).join('／');
   if(old.mainCardId==='JK206'||old.mainCardId==='JK206B')next=replace(next,[['查阅','读些什么']]);
   if(old.id==='GROUP_JK206'){
    next.structure='大牌组输出＋知识检索';
    next.notes='文字，文字，文字与读些什么取自《哈姆雷特》第二幕第二场的同一段读书问答。主牌运用整个牌组积累的字句，关联牌从抽牌堆选出当前需要的知识。获得时的成员及发放数量沿本组当前配置，各张牌独立使用和升级。';
   }
  }else if(rec.collection==='styles'){
   if(old.uid==='junna_style_art_review'){
    const rule='一般不使用空场景，只有场景本身与牌效完全契合时例外。优先保留能辨认的人物、物件与具体行动；攻击动作须能看清出手主体、持械手及武器方向，不能以模糊姿态代替动作。';
    if(!next.content.includes('一般不使用空场景'))next.content+='\n'+rule;
   }else{
    const record='2026年11月17日，按用户本轮改名、换图要求完成契合重选：JK206／JK206B为“文字，文字，文字／读些什么”，取《哈姆雷特》第二幕第二场同段问答，纯那阅读联系见2024年角色访谈；JK15改“再演”，取TV第9话奈奈对白；两张先古牌JZ03、JK127分别改“全力释放”“铭刻”，取《OVER THE スタァライト》的“全力で解き放て”与《百色リメイン》的“刻め”；JA96改“伤痕”，取《Star Darling》的“傷跡”。原名称保留在历史中。JK200沿用用户当前名称“呼喊”，同步旧“吹吧，狂风”引用。共13张牌完成本轮名称或卡图调整，来源、关联组和契合池同步，保留当前基础及升级牌面。';
    if(!next.content.includes('JK206／JK206B为“文字，文字，文字／读些什么”'))next.content+='\n'+record;
   }
  }
  const updates={},expectedFields={},expectedAbsent=[],unset=[];
  for(const k of new Set([...Object.keys(old),...Object.keys(next)])){
   if(same(old[k],next[k]))continue;
   if(!(k in next))unset.push(k);else updates[k]=next[k];
   if(k in old)expectedFields[k]=old[k];else expectedAbsent.push(k);
  }
  if(Object.keys(updates).length||unset.length)changes.push({collection:rec.collection,uid:old.uid,expectedVersion:rec.version,updates,expectedFields,...(expectedAbsent.length?{expectedAbsent}:{}),...(unset.length?{unset}:{})});
 }
 const assets={};
 for(const row of plan)if(row.asset)assets[row.asset]='data:image/jpeg;base64,'+fs.readFileSync(path.join(__dirname,'portraits',row.id+'.jpg')).toString('base64');
 const request={requestId:crypto.randomUUID(),clientId:'codex-knowledge-refit-20261117',label:'重选13张卡牌名称与原作卡图',changes,assets};
 save('commit-before-entities.json',records);save('commit-request.json',request);
 save('commit-review.json',{revision:state.revision,requestId:request.requestId,assets:Object.keys(assets),changes:changes.map(c=>({collection:c.collection,uid:c.uid,fields:Object.keys(c.updates),updates:c.updates}))});
 console.log(JSON.stringify({prepared:true,revision:state.revision,entities:records.length,changes:changes.length,assets:Object.keys(assets).length,requestId:request.requestId}));
}
async function apply(){
 const request=readFile('commit-request.json');
 const result=await callTool('workbench_commit',request);save('commit-receipt.json',result);
 console.log(JSON.stringify({saved:result.saved,revision:result.revision,keys:Object.keys(result),requestId:request.requestId}));
}
async function verify(){
 const before=readFile('commit-before-entities.json'),request=readFile('commit-request.json');
 const state=await callTool('workbench_get_state',{includeAssets:false,includeHistory:false,assetURLs:true});
 const after=request.changes.map(c=>({collection:c.collection,entity:state.bundle.data[c.collection].find(e=>e.uid===c.uid),version:state.versions[c.collection+'/'+c.uid]}));
 const expected=[];
 for(const change of request.changes){
  const rec=after.find(r=>r.collection===change.collection&&r.entity.uid===change.uid);if(!rec)throw Error('Missing readback');
  for(const [k,v]of Object.entries(change.updates))if(!same(rec.entity[k],v))throw Error('Readback mismatch '+change.uid+'.'+k);
  if(change.collection==='cards'){
   const old=before.find(r=>r.collection==='cards'&&r.entity.uid===change.uid).entity;
   for(const k of ['cost','upgradedCost','turns','upgradedTurns','rarity','type','pool','cardPool','target','arrowSubtype','keywords','partnerCardIds','effectIds','ruleIds','breakthroughIds'])if(!same(old[k],rec.entity[k]))throw Error('Mechanic changed '+old.id+'.'+k);
   for(const k of ['text','upgradedText'])if(replace(old[k],pairs)!==rec.entity[k]&&old[k]!==rec.entity[k])throw Error('Effect text changed '+old.id+'.'+k);
   if(target.has(old.id))expected.push({id:old.id,name:rec.entity.name,art:rec.entity.art});
  }
 }
 for(const row of plan)if(row.asset&&!state.assetIds.includes(row.asset))throw Error('Missing uploaded asset '+row.asset);
 save('commit-after-entities.json',after);save('verification.json',{passed:true,revision:state.revision,cards:expected,fieldsChecked:request.changes.length});
 console.log(JSON.stringify({passed:true,revision:state.revision,cards:expected,entries:after.length}));
}
({prepare,apply,verify}[process.argv[2]||'prepare']||(()=>{throw Error('Unknown mode')}))().catch(e=>{save('last-error.json',{code:e.code,message:e.message,conflicts:e.conflicts});console.error(JSON.stringify({code:e.code,message:e.message,conflicts:e.conflicts}));process.exitCode=1;});
