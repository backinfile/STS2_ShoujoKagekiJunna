'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../../..'),task=__dirname;
const {callTool}=require(path.resolve(process.argv[2],'docs/design/workbench/mcp-client.cjs'));
(async()=>{
 const card=await callTool('workbench_get_entity',{collection:'cards',id:'JK126'});
 const fit=await callTool('workbench_get_entity',{collection:'fitPool',id:'FIT_CARD_JK81B'});
 const manifest=JSON.parse(fs.readFileSync(path.join(task,'art-source.json'),'utf8'));
 const bytes=fs.readFileSync(path.join(root,manifest.output));
 const art=manifest.assetId;
 const artProof=manifest.source+'；裁切 0,0,960,720；成品 '+manifest.output;
 const naming={...card.entity.naming,artWork:'TV第9话约21:40—22:03，纯那拥住奈奈的大脸近景',artSourceUrl:manifest.sourceImage,artSourcePage:manifest.sourcePage,artProof,artProcessing:'原作无字幕1280×720近景原帧，向左保留环抱手臂，按原像素裁切为960×720。'};
 const updates=[{collection:'cards',uid:card.uid,expectedVersion:card.version,expectedFields:{art:card.entity.art,naming:card.entity.naming},updates:{art,naming}},
 {collection:'fitPool',uid:fit.uid,expectedVersion:fit.version,expectedFields:{art:fit.entity.art,notes:fit.entity.notes},updates:{art,notes:'卡图：TV第9话约21:40—22:03，纯那拥住奈奈的大脸近景。无字幕原帧来源：'+manifest.sourceImage+'；出处页：'+manifest.sourcePage+'；'+artProof}}];
 assert.equal(card.entity.art,'original-tv-jk126-full-embrace-20261117');
 assert.equal(fit.entity.art,'original-tv-jk126-full-embrace-20261117');
 const request={requestId:crypto.randomUUID(),clientId:'codex-jk126-full-embrace-20261117',label:'全都一起带去：保留原大脸拥抱镜头并放宽取景',kind:'编辑',changes:updates,assets:{[art]:'data:image/jpeg;base64,'+bytes.toString('base64')}};
 fs.writeFileSync(path.join(task,'workbench-changes.json'),JSON.stringify({...request,assets:undefined},null,2)+'\n');
 const receipt=await callTool('workbench_commit',request);
 fs.writeFileSync(path.join(task,'workbench-receipt.json'),JSON.stringify(receipt,null,2)+'\n');
 const next=await callTool('workbench_get_entity',{collection:'cards',id:'JK126'});
 const nextFit=await callTool('workbench_get_entity',{collection:'fitPool',id:'FIT_CARD_JK81B'});
 assert.deepEqual(next.entity,{...card.entity,art,naming});
 assert.deepEqual(nextFit.entity,{...fit.entity,...updates[1].updates});
 const result={revision:next.revision,cardVersion:next.version,fitVersion:nextFit.version,assetId:art,sha256:manifest.outputSha256,cardFieldsChanged:['art','naming.artWork','naming.artSourceUrl','naming.artSourcePage','naming.artProof','naming.artProcessing'],fitFieldsChanged:['art','notes'],unchangedCardFieldsVerified:true,unchangedFitFieldsVerified:true};
 fs.writeFileSync(path.join(task,'verification.json'),JSON.stringify(result,null,2)+'\n');
 console.log(JSON.stringify(result));
})().catch(error=>{console.error(error.code||error.message);process.exitCode=1;});
