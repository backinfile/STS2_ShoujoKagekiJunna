'use strict';
const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync(require.resolve('./card-model-view.js'),'utf8');

function fixture({aoe=true,section='vanilla',nativeCards=[]}={}){
  const ui={modelSection:section,modelKind:aoe?'aoe':'all'};
  const cards=[
    {uid:'single',kind:'attack',name:'攻击',cost:1,turns:0,text:'造成10点伤害。',upgradedText:'造成14点伤害。',modelValue:10,upgradedModelValue:14},
    ...(aoe?[{uid:'group',kind:'aoe',name:'群体攻击',cost:1,turns:0,text:'对所有敌人造成8点伤害。',upgradedText:'对所有敌人造成11点伤害。',modelValue:8,upgradedModelValue:11}]:[])
  ];
  const parameters={attack:{1:{base:10,upgraded:14,byTurns:Object.fromEntries([0,1,2,3].map(t=>[t,{base:10+4*t,upgraded:14+6*t}]))}},...(aoe?{aoe:{1:{base:8,upgraded:11,byTurns:Object.fromEntries([0,1,2,3].map(t=>[t,{base:8+3*t,upgraded:11+5*t}]))}}}:{})};
  const data={meta:{vanillaCardModels:{cards,parameters,basis:{aoe:{status:'paperEstimate'}}},nativeCardModels:{date:'2026-11-08',cards:nativeCards}},cards:[]};
  const nodes=new Map();const dom=id=>{if(!nodes.has(id))nodes.set(id,{innerHTML:'',textContent:'',removeAttribute(){},setAttribute(){}});return nodes.get(id);};
  const context=vm.createContext({document:{getElementById(){return null;}}});vm.runInContext(source,context);
  const view=context.createJunnaCardModelView({data:()=>data,ui:()=>ui,dom,escape:value=>String(value),render(){},cardHTML:(card,upgraded)=>'<div class="game-card">'+(upgraded?card.upgradedText:card.text)+'</div>',fitCards(){}});
  return {view,ui,dom,data};
}

test('group attack filtering renders the base and upgraded all-enemy model',()=>{
  const f=fixture();assert.match(f.view.sidebar(),/data-model-value="aoe"[^>]*aria-pressed="true"/);
  f.view.workspace();assert.match(f.dom('work-area').innerHTML,/对所有敌人造成8点伤害/);
  assert.doesNotMatch(f.dom('work-area').innerHTML,/data-model-card="single"/);
  f.ui.modelUpgraded=true;f.view.workspace();assert.match(f.dom('work-area').innerHTML,/对所有敌人造成11点伤害/);
});

test('group attack inspector reports damage and its all-enemy target',()=>{
  const f=fixture();f.view.inspector();
  assert.match(f.dom('editor-form').innerHTML,/全体敌人/);
  assert.match(f.dom('editor-form').innerHTML,/基础伤害/);
  assert.match(f.dom('editor-form').innerHTML,/升级伤害/);
  assert.doesNotMatch(f.dom('editor-form').innerHTML,/格挡/);
});

test('quick reference adds the group curve and still opens legacy model snapshots',()=>{
  const current=fixture({section:'quick'});current.view.workspace();
  assert.match(current.dom('work-area').innerHTML,/群体伤害（每名敌人）/);
  const legacy=fixture({aoe:false,section:'quick'});legacy.view.workspace();
  assert.doesNotMatch(legacy.dom('work-area').innerHTML,/群体伤害（每名敌人）/);
});

test('two and three hit samples split the total budget and show the total',()=>{
  const f=fixture({aoe:false});f.ui.modelKind='attack';f.ui.modelHits='2';
  assert.match(f.view.sidebar(),/data-model-filter="modelHits"/);
  f.view.workspace();assert.match(f.dom('work-area').innerHTML,/造成5点伤害2次/);
  f.ui.modelUpgraded=true;f.view.workspace();assert.match(f.dom('work-area').innerHTML,/造成7点伤害2次/);
  f.view.inspector();assert.match(f.dom('editor-form').innerHTML,/总伤害/);
  assert.match(f.dom('editor-form').innerHTML,/10／14/);
  f.ui.modelHits='3';f.view.workspace();assert.match(f.dom('work-area').innerHTML,/造成4点伤害3次/);
  f.view.inspector();assert.match(f.dom('editor-form').innerHTML,/9／12/);
});

const nativeCards=[
  {id:'TWIN_STRIKE',name:'双重打击',pool:'铁甲战士',rarity:'普通',type:'攻击',cost:1,upgradedCost:1,text:'造成5点伤害两次。',upgradedText:'造成7点伤害两次。',keywords:[],upgradedKeywords:[],tags:['伤害'],metric:{kind:'attack',value:5,hits:2,total:10},upgradedMetric:{kind:'attack',value:7,hits:2,total:14}},
  {id:'SKIM',name:'快速检索',pool:'故障机器人',rarity:'罕见',type:'技能',cost:1,upgradedCost:1,text:'抽3张牌。',upgradedText:'抽4张牌。',keywords:[],upgradedKeywords:[],tags:['抽牌']},
  {id:'SECRET_WEAPON',name:'秘密武器',pool:'无色',rarity:'罕见',type:'技能',cost:0,upgradedCost:0,text:'选择一张攻击牌。',upgradedText:'选择一张攻击牌。',keywords:['消耗'],upgradedKeywords:[],tags:['其他']}
];
test('native catalog renders both faces and filters by effect and pool',()=>{
  const f=fixture({section:'native',nativeCards});f.view.workspace();
  assert.match(f.dom('result-count').textContent,/3／3张/);
  assert.match(f.dom('work-area').innerHTML,/data-native-card="SKIM"/);
  assert.match(f.dom('work-area').innerHTML,/抽4张牌/);
  f.ui.nativeModelTag='抽牌';f.view.workspace();
  assert.match(f.dom('work-area').innerHTML,/data-native-card="SKIM"/);
  assert.doesNotMatch(f.dom('work-area').innerHTML,/data-native-card="TWIN_STRIKE"/);
  f.ui.nativeModelTag='all';f.ui.nativeModelPool='铁甲战士';f.view.workspace();f.view.inspector();
  assert.match(f.dom('editor-form').innerHTML,/2段/);
  assert.match(f.dom('editor-form').innerHTML,/10／14/);
});
test('native upgrade keyword removal remains visible and missing snapshots are supported',()=>{
  const f=fixture({section:'native',nativeCards});f.ui.nativeModelSelected='SECRET_WEAPON';f.view.inspector();
  assert.match(f.dom('editor-form').innerHTML,/基础关键词/);
  assert.match(f.dom('editor-form').innerHTML,/消耗/);
  assert.match(f.dom('editor-form').innerHTML,/升级关键词/);
  const empty=fixture({section:'native'});empty.view.workspace();empty.view.inspector();
  assert.match(empty.dom('work-area').innerHTML,/暂无本体卡牌/);
});

test('hit filters include mixed attacks and exclude block cards',()=>{
  const f=fixture({section:'native',nativeCards:[...nativeCards,
    {id:'POMMEL_STRIKE',name:'剑柄打击',pool:'铁甲战士',rarity:'普通',type:'攻击',cost:1,upgradedCost:1,text:'造成9点伤害。抽1张牌。',upgradedText:'造成10点伤害。抽2张牌。',keywords:[],upgradedKeywords:[],tags:['伤害','抽牌'],hitCount:1},
    {id:'DEFLECT',name:'偏折',pool:'静默猎手',rarity:'普通',type:'技能',cost:0,upgradedCost:0,text:'获得4点格挡。',upgradedText:'获得7点格挡。',keywords:[],upgradedKeywords:[],tags:['格挡'],hitCount:null,metric:{kind:'block',hits:1,total:4}}
  ]});
  f.ui.nativeModelHits='1';f.view.workspace();
  assert.match(f.dom('work-area').innerHTML,/data-native-card="POMMEL_STRIKE"/);
  assert.doesNotMatch(f.dom('work-area').innerHTML,/data-native-card="DEFLECT"/);
  const samples=fixture();samples.data.meta.vanillaCardModels.cards.push({uid:'block',kind:'block',name:'防御',cost:1,turns:0,text:'获得9点格挡。'});
  samples.ui.modelKind='all';samples.ui.modelHits='2';samples.view.workspace();
  assert.doesNotMatch(samples.dom('work-area').innerHTML,/data-model-card="block"/);
});

test('calibrated group samples use their own two-hit anchor and display its basis',()=>{
  const f=fixture();f.ui.modelHits='2';f.ui.modelUpgraded=true;
  f.data.meta.vanillaCardModels.basis.calibration={};
  f.data.meta.vanillaCardModels.cards[1].notes='单段备注不应被多段继承';
  f.data.meta.vanillaCardModels.parameters.aoe[1].byHits={2:{reference:{ids:['DAGGER_SPRAY'],status:'nativeExact',note:'匕首雨：4×2／6×2。'},byTurns:{0:{base:8,upgraded:12,perHitBase:4,perHitUpgraded:6,roundingLoss:[0,0],notes:'计算来源：数值直接来自本体。匕首雨：4×2／6×2。'}}}};
  f.view.workspace();f.view.inspector();
  assert.match(f.dom('work-area').innerHTML,/造成6点伤害2次/);
  assert.match(f.dom('editor-form').innerHTML,/8／12/);
  assert.match(f.dom('editor-form').innerHTML,/匕首雨：4×2／6×2/);
  assert.match(f.dom('editor-form').innerHTML,/<h3>备注<\/h3>/);assert.doesNotMatch(f.dom('editor-form').innerHTML,/单段备注不应被多段继承/);
});

test('delayed multi-hit examples expose the unrounded budget used by the tier',()=>{
  const f=fixture({aoe:false});f.ui.modelKind='attack';f.ui.modelHits='3';f.ui.modelTurns='1';
  f.data.meta.vanillaCardModels.cards.push({uid:'delayed',kind:'attack',name:'攻击',cost:1,turns:1,text:'造成15点伤害。',upgradedText:'造成21点伤害。',modelValue:15,upgradedModelValue:21});
  f.data.meta.vanillaCardModels.basis.calibration={};
  f.data.meta.vanillaCardModels.parameters.attack[1].byHits={3:{reference:{ids:[],status:'estimate',note:''},byTurns:{0:{base:9,upgraded:12},1:{base:15,upgraded:21,perHitBase:5,perHitUpgraded:7,instantBudget:[10,14],roundingLoss:[0,0]}}}};
  f.view.workspace();f.view.inspector();
  assert.match(f.dom('editor-form').innerHTML,/分段前即时预算/);assert.match(f.dom('editor-form').innerHTML,/10／14/);assert.match(f.dom('editor-form').innerHTML,/15／21/);
});

test('reference system presents confirmed models, conditional formulas and excluded auxiliary effects',()=>{
  const f=fixture({section:'basis',nativeCards});
  f.data.meta.vanillaCardModels.basis.calibration={referenceSystem:{
    confirmedModels:[{label:'1费即时两段单体',values:'5×2／7×2',id:'TWIN_STRIKE'}],
    anchorIds:['TWIN_STRIKE'],
    formulaRows:[['单体＝群伤×配对比','×10/8＝×1.25','×14/12≈×1.1667','迁移到其他条件为推算']],
    excluded:[{id:'TWIN_STRIKE',reason:'附效检查的测试内容'}],
    multiHitAudit:{findings:['扣减曲线缺少独立单段对照'],rows:[['双重打击','1费 · 普通','5×2／7×2','本体原值，不能独立确定扣减']]}
  }};
  assert.match(f.view.sidebar(),/基准与公式/);f.view.workspace();
  const html=f.dom('work-area').innerHTML;
  assert.match(html,/可直接确认的模板/);assert.match(html,/1费即时两段单体/);assert.match(html,/5×2／7×2/);
  assert.match(html,/双重打击/);assert.match(html,/×10\/8＝×1.25/);assert.match(html,/迁移到其他条件为推算/);
  assert.match(html,/附效检查的测试内容/);
  assert.match(html,/多段规则核对/);assert.match(html,/扣减曲线缺少独立单段对照/);assert.match(html,/本体原值，不能独立确定扣减/);
});

test('multi-hit inspector separates the hit premium from integer rounding',()=>{
  const f=fixture({aoe:false});f.ui.modelKind='attack';f.ui.modelHits='3';
  f.data.meta.vanillaCardModels.parameters.attack[1].byHits={3:{byTurns:{0:{base:6,upgraded:9,perHitBase:2,perHitUpgraded:3,hitPenalty:3,roundingLoss:[1,2],notes:'段数扣减为设计参数；3段扣3点，再取整。'}}}};
  f.view.workspace();f.view.inspector();
  assert.match(f.dom('work-area').innerHTML,/造成2点伤害3次/);
  assert.match(f.dom('editor-form').innerHTML,/段数扣减<\/dt><dd>3／3/);
  assert.match(f.dom('editor-form').innerHTML,/分段取整减少<\/dt><dd>1／2/);
});

function poolAuditFixture(){
  const f=fixture({section:'cards'}),card={uid:'audit1',id:'AUDIT1',name:'当前牌',type:'攻击',rarity:'普通',pool:true,cost:1,upgradedCost:1,turns:null,upgradedTurns:null,text:'造成7点伤害。',upgradedText:'造成10点伤害。',keywords:[],notes:'',design:{rules:''}};
  f.data.cards=[card];f.data.partnerGroups=[];
  const keys=['type','rarity','pool','cost','upgradedCost','turns','upgradedTurns','text','upgradedText'];
  const row={id:card.id,status:'建议调整',priority:1,basis:'当前逐张分析',recommendation:'建议试调',snapshot:Object.fromEntries(keys.map(k=>[k,card[k]??null])),metadata:{rules:'',notes:'',keywords:[]},groupSnapshot:[],dependencies:[]};
  f.data.meta.cardModelAudit={date:'2026-11-08',snapshotKeys:keys,rows:[row]};return{...f,card,row};
}
test('pool review reads live MCP metadata and preserves stale recommendations as previous results',()=>{
  const f=poolAuditFixture();f.view.workspace();assert.match(f.dom('work-area').innerHTML,/建议调整/);assert.match(f.dom('work-area').innerHTML,/当前逐张分析/);assert.match(f.dom('work-area').innerHTML,/2026-11-08/);
  f.card.text='造成9点伤害。';f.view.workspace();assert.match(f.dom('work-area').innerHTML,/待复核/);assert.match(f.dom('work-area').innerHTML,/上次结论/);
});
test('pool review becomes stale when model context or dependency rules change',()=>{
  const f=poolAuditFixture();f.data.meta.damageModifierRules={version:2};f.data.meta.cardModelAudit.contextSnapshot={damageModifierRules:{version:2}};
  f.data.cards.push({uid:'dep',id:'DEP',type:'能力',text:'持续效果。',design:{rules:'消耗优先'},notes:'',keywords:[]});
  f.row.dependencies=[{id:'DEP',snapshot:Object.fromEntries(f.data.meta.cardModelAudit.snapshotKeys.map(k=>[k,f.data.cards[1][k]??null])),metadata:{rules:'消耗优先',notes:'',keywords:[]}}];
  f.view.workspace();assert.match(f.dom('work-area').innerHTML,/data-audit-card="AUDIT1" data-audit-status="建议调整"/);
  f.data.cards[1].design.rules='重新打出优先';f.view.workspace();assert.match(f.dom('work-area').innerHTML,/data-audit-card="AUDIT1" data-audit-status="待复核"/);
  f.data.cards[1].design.rules='消耗优先';f.data.meta.damageModifierRules.version=3;f.view.workspace();assert.match(f.dom('work-area').innerHTML,/data-audit-card="AUDIT1" data-audit-status="待复核"/);
});
