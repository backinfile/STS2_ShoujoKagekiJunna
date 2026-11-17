'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const {callTool}=require(path.resolve(process.argv[2]));
const base=__dirname;
(async()=>{
 const runtime=path.join(base,'review');fs.mkdirSync(runtime,{recursive:true});
 const requestPath=path.join(runtime,'kaien-request.json');let request;
 if(fs.existsSync(requestPath))request=JSON.parse(fs.readFileSync(requestPath,'utf8'));
 else {
  const card=await callTool('workbench_get_entity',{collection:'cards',id:'JA71'});
  const fit=await callTool('workbench_get_entity',{collection:'fitPool',id:'FIT_CARD_JA71'});
  const matches=await callTool('workbench_query',{collection:'cards',search:'开演',includeArchived:true,limit:100});
  if(matches.entries.some(x=>x.entity.name==='开演'&&x.entity.id!=='JA71'))throw Error('Name occupied');
  const aid='original-tv-ja71-kaien-window-lights-20261117';
  const adaptation='纯那掌控的舞台圆窗同时亮起，表示此前安排的行动正式进入演出。所有已经布置的飞矢按结算顺序立即完成，格挡承接舞台装置对当前站位的保护。';
  const naming={original:'始まりました',translation:'开演了',chineseOriginal:'',kind:'用户指定中文名；原作歌词舞台语境',work:'《舞台プレパレイション》；演唱：スタァライト九九組；作词：中村彼方',url:'https://www.uta-net.com/song/261292/',adaptation,verification:'2026-11-17用户选定卡名“开演”。原词在曲末由准备转入实际开场的段落出现。',artWork:'TV第2话·渴望的Revue，纯那掌控的舞台圆窗发出紫色光芒',artSourceUrl:'https://xenodude.com/2024/06/19/revue-starlight-ep-2/',artProof:'docs/worktree-tasks/xiansheng-qiming-fit-20261117/sources/xenodude-tv02-21.png；原图1920×1080，裁切200,0,920,690，输出960×720；精确播放秒数未定位。',artOriginalUrl:'https://xenodude.com/wp-content/uploads/2024/06/revue-starlight-ep.-2-21.png',artProcessing:'无字幕原帧裁切并等比例缩放。'};
  const design=structuredClone(card.entity.design);design.rules=design.rules.replaceAll('10/15','12/16');design.upgrade=design.upgrade.replaceAll('10→15','12→16');
  const updates={name:'开演',art:aid,naming,design,notes:card.entity.notes.replaceAll('10/15','12/16').replaceAll('10→15','12→16')};
  const fitUpdates={name:'开演',theme:'舞台掌控：开演',text:adaptation,original:'始まりました',source:naming.work,sourceUrl:naming.url,art:aid,notes:naming.artWork+'。配图来源：'+naming.artSourceUrl+'；'+naming.artProof};
  function change(collection,current,values){return {collection,uid:current.uid,expectedVersion:current.version,updates:values,expectedFields:Object.fromEntries(Object.keys(values).filter(k=>Object.hasOwn(current.entity,k)).map(k=>[k,current.entity[k]])),expectedAbsent:Object.keys(values).filter(k=>!Object.hasOwn(current.entity,k))};}
  request={clientId:'codex-xiansheng-qiming-fit-20261117',requestId:crypto.randomUUID(),kind:'edit',label:'按用户选定将弦声齐鸣改为开演并同步舞台圆窗卡图',changes:[change('cards',card,updates),change('fitPool',fit,fitUpdates)],assets:{[aid]:'data:image/png;base64,'+fs.readFileSync(path.join(base,'stage-lights-candidate.png')).toString('base64')}};
  fs.writeFileSync(requestPath,JSON.stringify(request));
 }
 const result=await callTool('workbench_commit',request);fs.writeFileSync(path.join(base,'kaien-save-receipt.json'),JSON.stringify(result,null,2)+'\n');
 const readback=await callTool('workbench_get_entity',{collection:'cards',id:'JA71'});const fitback=await callTool('workbench_get_entity',{collection:'fitPool',id:'FIT_CARD_JA71'});
 if(readback.entity.name!=='开演'||readback.entity.art!==request.changes[0].updates.art||readback.entity.text!=='获得12点格挡。所有飞矢加速到完成。'||readback.entity.upgradedText!=='获得16点格挡。所有飞矢加速到完成。'||fitback.entity.name!=='开演')throw Error('Readback mismatch');
 fs.writeFileSync(path.join(base,'kaien-readback.json'),JSON.stringify({card:readback,fitPool:fitback},null,2)+'\n');console.log(JSON.stringify({saved:result.saved,revision:result.revision,name:readback.entity.name,version:readback.version,requestId:request.requestId}));
})().catch(e=>{console.error(JSON.stringify({code:e.code,message:e.message,conflicts:e.conflicts}));process.exitCode=1;});