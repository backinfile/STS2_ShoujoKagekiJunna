'use strict';
const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync(require.resolve('./card-model-view.js'),'utf8');

function fixture({aoe=true,section='vanilla'}={}){
  const ui={modelSection:section,modelKind:aoe?'aoe':'all'};
  const cards=[
    {uid:'single',kind:'attack',name:'攻击',cost:1,turns:0,text:'造成10点伤害。',upgradedText:'造成14点伤害。',modelValue:10,upgradedModelValue:14},
    ...(aoe?[{uid:'group',kind:'aoe',name:'群体攻击',cost:1,turns:0,text:'对所有敌人造成8点伤害。',upgradedText:'对所有敌人造成11点伤害。',modelValue:8,upgradedModelValue:11}]:[])
  ];
  const parameters={attack:{1:{base:10,upgraded:14,byTurns:Object.fromEntries([0,1,2,3].map(t=>[t,{base:10+4*t,upgraded:14+6*t}]))}},...(aoe?{aoe:{1:{base:8,upgraded:11,byTurns:Object.fromEntries([0,1,2,3].map(t=>[t,{base:8+3*t,upgraded:11+5*t}]))}}}:{})};
  const data={meta:{vanillaCardModels:{cards,parameters,basis:{aoe:{status:'paperEstimate'}}}},cards:[]};
  const nodes=new Map();const dom=id=>{if(!nodes.has(id))nodes.set(id,{innerHTML:'',textContent:'',removeAttribute(){},setAttribute(){}});return nodes.get(id);};
  const context=vm.createContext({document:{getElementById(){return null;}}});vm.runInContext(source,context);
  const view=context.createJunnaCardModelView({data:()=>data,ui:()=>ui,dom,escape:value=>String(value),render(){},cardHTML:(card,upgraded)=>'<div class="game-card">'+(upgraded?card.upgradedText:card.text)+'</div>',fitCards(){}});
  return {view,ui,dom};
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
